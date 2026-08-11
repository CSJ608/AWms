using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AWms.Infrastructure.Services;

/// <summary>
/// 查询平台：filter DSL（13 操作符）+ sort 白名单 + 排序唯一性兜底（规范 §3.2）。
/// 修复点（复验意见 B-04）：contains/startsWith 的 JsonElement 转换、isNull 值类型、
/// between 日期上界（DateOnly +1 天）、嵌套 and/or 合并、主数据默认排序 id 兜底、白名单外 400。
/// </summary>
public class QueryService : IQueryService
{
    public async Task<(IQueryable<T> Query, PagedResult<T> Result)> ApplyAsync<T>(
        IQueryable<T> source,
        FilterRequest request,
        IReadOnlySet<string> fieldWhitelist,
        IReadOnlySet<string> sortWhitelist,
        string defaultSortField,
        string defaultSortDir = "asc",
        bool isTimeBasedList = false) where T : class
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            source = ApplyKeyword(source, request.Keyword);

        if (request.Filter != null)
            source = ApplyFilterGroup(source, request.Filter, fieldWhitelist);

        source = ApplySorting(source, request.Sort, sortWhitelist, defaultSortField, defaultSortDir, isTimeBasedList);

        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = request.PageSize ?? 20;
        if (pageSize < 0) pageSize = 20;

        var total = await source.CountAsync();

        if (pageSize > 0)
            source = source.Skip((page - 1) * pageSize).Take(pageSize);
        else
            page = 1;

        var items = await source.ToListAsync();
        var result = new PagedResult<T>(items.AsReadOnly(), total, page, pageSize > 0 ? pageSize : total);
        return (source, result);
    }

    private static IQueryable<T> ApplyFilterGroup<T>(IQueryable<T> source, FilterGroup group, IReadOnlySet<string> fieldWhitelist) where T : class
    {
        var param = Expression.Parameter(typeof(T), "x");
        var expr = BuildFilterExpression<T>(group, param, fieldWhitelist);
        return expr == null ? source : source.Where(Expression.Lambda<Func<T, bool>>(expr, param));
    }

    /// <summary>构建谓词表达式：条件 + 嵌套 and/or 组统一合并（复验意见：嵌套组不得丢弃）。</summary>
    private static Expression? BuildFilterExpression<T>(FilterGroup group, ParameterExpression param, IReadOnlySet<string> fieldWhitelist) where T : class
    {
        Expression? combined = null;

        foreach (var cond in group.Conditions)
        {
            if (string.IsNullOrWhiteSpace(cond.Field))
                throw new DomainException("VALIDATION_ERROR", "filter 条件缺少 field", 400);

            if (!fieldWhitelist.Contains(cond.Field))
                throw new DomainException("VALIDATION_ERROR", $"Field '{cond.Field}' is not allowed for filtering", 400);

            if (!Enum.TryParse<FilterOperator>(cond.Op, out var op))
                throw new DomainException("VALIDATION_ERROR", $"Operator '{cond.Op}' is not supported", 400);

            var prop = typeof(T).GetProperty(UpperFirst(cond.Field));
            if (prop == null)
                throw new DomainException("VALIDATION_ERROR", $"Field '{cond.Field}' is not supported on {typeof(T).Name}", 400);

            var member = Expression.Property(param, prop);
            var expr = BuildExpression(member, prop.PropertyType, op, cond.Value);
            if (expr == null) continue;

            combined = combined == null ? expr
                : (IsOr(group.Op) ? Expression.OrElse(combined, expr) : Expression.AndAlso(combined, expr));
        }

        if (group.Groups != null)
        {
            foreach (var sub in group.Groups)
            {
                var subExpr = BuildFilterExpression<T>(sub, param, fieldWhitelist);
                if (subExpr == null) continue;
                combined = combined == null ? subExpr
                    : (IsOr(group.Op) ? Expression.OrElse(combined, subExpr) : Expression.AndAlso(combined, subExpr));
            }
        }

        return combined;
    }

    private static bool IsOr(string? op) => string.Equals(op, "or", StringComparison.OrdinalIgnoreCase);

    private static Expression? BuildExpression(MemberExpression member, Type propertyType, FilterOperator op, object? value)
    {
        switch (op)
        {
            case FilterOperator.eq:
            case FilterOperator.neq:
            {
                var constant = ToConstant(value, propertyType);
                return op == FilterOperator.eq
                    ? Expression.Equal(member, constant)
                    : Expression.NotEqual(member, constant);
            }
            case FilterOperator.contains:
            case FilterOperator.startsWith:
            {
                var s = ExtractString(value)
                    ?? throw new DomainException("VALIDATION_ERROR", $"Operator '{op}' 需要字符串值", 400);
                if (propertyType != typeof(string))
                    throw new DomainException("VALIDATION_ERROR", $"Operator '{op}' 仅支持字符串字段", 400);
                var method = propertyType.GetMethod(op == FilterOperator.contains ? "Contains" : "StartsWith", [typeof(string)]);
                return Expression.Call(member, method!, Expression.Constant(s));
            }
            case FilterOperator.gt:
            case FilterOperator.gte:
            case FilterOperator.lt:
            case FilterOperator.lte:
            {
                var constant = ToConstant(value, propertyType);
                return op switch
                {
                    FilterOperator.gt => Expression.GreaterThan(member, constant),
                    FilterOperator.gte => Expression.GreaterThanOrEqual(member, constant),
                    FilterOperator.lt => Expression.LessThan(member, constant),
                    _ => Expression.LessThanOrEqual(member, constant)
                };
            }
            case FilterOperator.isNull:
            case FilterOperator.isNotNull:
            {
                if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null)
                {
                    // 非空值类型不可能为 null：isNull 恒 false / isNotNull 恒 true（避免 Expression.Constant(null, 值类型) 500）
                    return Expression.Constant(op != FilterOperator.isNull);
                }
                var nullConstant = Expression.Constant(null, propertyType);
                return op == FilterOperator.isNull
                    ? Expression.Equal(member, nullConstant)
                    : Expression.NotEqual(member, nullConstant);
            }
            case FilterOperator.between:
                return BuildBetween(member, propertyType, value);
            case FilterOperator.@in:
                return BuildIn(member, propertyType, value, negate: false);
            case FilterOperator.notIn:
                return BuildIn(member, propertyType, value, negate: true);
            default:
                throw new DomainException("VALIDATION_ERROR", $"Operator '{op}' is not supported", 400);
        }
    }

    private static Expression? BuildBetween(MemberExpression member, Type memberType, object? value)
    {
        var arr = ExtractArray(value)
            ?? throw new DomainException("VALIDATION_ERROR", "between 需要 [lower, upper] 数组", 400);
        if (arr.Length != 2)
            throw new DomainException("VALIDATION_ERROR", "between 需要恰好 2 个值", 400);

        var target = Nullable.GetUnderlyingType(memberType) ?? memberType;
        var lowerRaw = ConvertScalar(arr[0], target);
        var upperRaw = ConvertScalar(arr[1], target);

        if (target == typeof(DateOnly))
        {
            // 纯日期 between：上界含当天（>= lower && < upper.AddDays(1)）（复验意见）
            var upperDate = (DateOnly)upperRaw!;
            var lowerConst = ToConstant(lowerRaw, memberType);
            var upperExclusive = ToConstant(upperDate.AddDays(1), memberType);
            return Expression.AndAlso(
                Expression.GreaterThanOrEqual(member, lowerConst),
                Expression.LessThan(member, upperExclusive));
        }

        var lower = ToConstant(lowerRaw, memberType);
        var upper = ToConstant(upperRaw, memberType);
        return Expression.AndAlso(
            Expression.GreaterThanOrEqual(member, lower),
            Expression.LessThanOrEqual(member, upper));
    }

    private static Expression? BuildIn(MemberExpression member, Type memberType, object? value, bool negate)
    {
        var arr = ExtractArray(value)
            ?? throw new DomainException("VALIDATION_ERROR", "in/notIn 需要数组值", 400);
        if (arr.Length == 0)
            throw new DomainException("VALIDATION_ERROR", "in/notIn 数组不能为空", 400);

        var listType = typeof(List<>).MakeGenericType(memberType);
        var list = Activator.CreateInstance(listType)!;
        var addMethod = listType.GetMethod("Add")!;
        foreach (var item in arr)
        {
            var converted = ToConstant(item, memberType).Value!;
            addMethod.Invoke(list, [converted]);
        }

        var containsMethod = listType.GetMethod("Contains")!;
        var call = Expression.Call(Expression.Constant(list, listType), containsMethod, member);
        return negate ? Expression.Not(call) : call;
    }

    /// <summary>把 JsonElement/原始值转换为目标 CLR 类型（复验意见：JsonElement 转换）。</summary>
    private static ConstantExpression ToConstant(object? value, Type memberType)
    {
        var target = Nullable.GetUnderlyingType(memberType) ?? memberType;
        var converted = ConvertScalar(value, target);
        if (memberType.IsValueType && Nullable.GetUnderlyingType(memberType) != null)
        {
            // Nullable<T> 需要显式构造（Expression.Constant(T, Nullable<T>) 不允许）
            var boxed = Activator.CreateInstance(memberType, converted);
            return Expression.Constant(boxed, memberType);
        }
        return Expression.Constant(converted, memberType);
    }

    private static object ConvertScalar(object? value, Type target)
    {
        var raw = value is JsonElement je ? ExtractJsonScalar(je) : value;
        if (raw == null) return null!;

        var s = raw as string;
        if (target == typeof(string)) return s ?? raw.ToString()!;
        if (target == typeof(decimal)) return decimal.Parse(s ?? raw.ToString()!, CultureInfo.InvariantCulture);
        if (target == typeof(int)) return int.Parse(s ?? raw.ToString()!, CultureInfo.InvariantCulture);
        if (target == typeof(long)) return long.Parse(s ?? raw.ToString()!, CultureInfo.InvariantCulture);
        if (target == typeof(double)) return double.Parse(s ?? raw.ToString()!, CultureInfo.InvariantCulture);
        if (target == typeof(bool)) return bool.Parse(s ?? raw.ToString()!);
        if (target == typeof(DateTime)) return DateTime.Parse(s ?? raw.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        if (target == typeof(DateOnly)) return DateOnly.Parse(s ?? raw.ToString()!, CultureInfo.InvariantCulture);
        if (target == typeof(Guid)) return Guid.Parse(s ?? raw.ToString()!);
        if (target.IsEnum) return Enum.Parse(target, s ?? raw.ToString()!, ignoreCase: true);
        return raw;
    }

    private static object? ExtractJsonScalar(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.String => je.GetString(),
        JsonValueKind.Number when je.TryGetDecimal(out var dec) => dec,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => je.ToString()
    };

    private static string? ExtractString(object? value) => value switch
    {
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
        JsonElement { ValueKind: JsonValueKind.Number } je => je.GetDecimal().ToString(CultureInfo.InvariantCulture),
        JsonElement { ValueKind: JsonValueKind.True } => "true",
        JsonElement { ValueKind: JsonValueKind.False } => "false",
        null => null,
        _ => value.ToString()
    };

    private static JsonElement[]? ExtractArray(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.Array } je => je.EnumerateArray().ToArray(),
        _ => null
    };

    private static string UpperFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static IQueryable<T> ApplySorting<T>(
        IQueryable<T> source,
        List<SortOption>? sortOptions,
        IReadOnlySet<string> sortWhitelist,
        string defaultSortField,
        string defaultSortDir,
        bool isTimeBasedList) where T : class
    {
        string field;
        string dir;
        var userSort = sortOptions != null && sortOptions.Count > 0;
        if (userSort)
        {
            var opt = sortOptions![0];
            if (!sortWhitelist.Contains(opt.Field))
                throw new DomainException("VALIDATION_ERROR", $"Sort field '{opt.Field}' is not allowed", 400);
            if (opt.Dir is not "asc" and not "desc")
                throw new DomainException("VALIDATION_ERROR", $"Sort dir must be asc/desc, got '{opt.Dir}'", 400);
            field = opt.Field;
            dir = opt.Dir;
        }
        else
        {
            field = defaultSortField;
            dir = defaultSortDir;
        }

        var param = Expression.Parameter(typeof(T), "x");
        var orderProp = typeof(T).GetProperty(UpperFirst(field))
            ?? throw new DomainException("VALIDATION_ERROR", $"Sort field '{field}' is not supported on {typeof(T).Name}", 400);
        var orderExp = Expression.Property(param, orderProp);

        var desc = dir == "desc";
        source = ApplyOrder(source, orderExp, desc);

        // 排序唯一性兜底（规范 §3.2）：主数据默认 id asc；用户自定义/时间性默认 id DESC
        var idProp = typeof(T).GetProperty("Id");
        if (idProp != null)
        {
            var idExp = Expression.Property(param, idProp);
            var idDesc = userSort || isTimeBasedList;
            source = ApplyThenBy(source, idExp, idDesc);
        }

        return source;
    }

    private static IQueryable<T> ApplyOrder<T>(IQueryable<T> source, MemberExpression keyExpr, bool desc) where T : class
    {
        var method = desc ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
        var lambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(T), keyExpr.Type), keyExpr, (ParameterExpression)keyExpr.Expression!);
        return (IQueryable<T>)typeof(Queryable).GetMethods()
            .First(m => m.Name == method && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), keyExpr.Type)
            .Invoke(null, [source, lambda])!;
    }

    private static IQueryable<T> ApplyThenBy<T>(IQueryable<T> source, MemberExpression keyExpr, bool desc) where T : class
    {
        var method = desc ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);
        var lambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(typeof(T), keyExpr.Type), keyExpr, (ParameterExpression)keyExpr.Expression!);
        return (IQueryable<T>)typeof(Queryable).GetMethods()
            .First(m => m.Name == method && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), keyExpr.Type)
            .Invoke(null, [source, lambda])!;
    }

    protected virtual IQueryable<T> ApplyKeyword<T>(IQueryable<T> source, string keyword) where T : class => source;
}






