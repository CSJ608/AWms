using System.Data;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.ImportExport;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AWms.Infrastructure.Services;

/// <summary>
/// 导入导出（复验意见 B-06/B-07 修复版）：
/// - precheck 不落业务数据，failures inline ≤200；
/// - execute 同一事务内重校验唯一性 + 真实入库（全部通过才执行，失败全回滚）；
/// - 导出异步 PROCESSING→DONE，filter/sort/pageSize 全部生效，后台任务用独立 DbContext 作用域。
/// </summary>
public class ImportExportService
{
    private const int MaxInlineFailures = 200;

    private readonly AWmsDbContext _db;
    private readonly INumberService _numbering;
    private readonly IQueryService _queryService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImportExportService> _logger;

    private static readonly HashSet<string> MaterialFields = new(StringComparer.Ordinal) { "code", "name", "searchCode", "batchControlled", "labelType", "defaultUom", "defaultQtyPerLabel", "status", "createdAt", "updatedAt" };
    private static readonly HashSet<string> MaterialSorts = new(StringComparer.Ordinal) { "code", "name", "status", "updatedAt" };

    public ImportExportService(
        AWmsDbContext db,
        INumberService numbering,
        IQueryService queryService,
        IServiceScopeFactory scopeFactory,
        ILogger<ImportExportService> logger)
    {
        _db = db;
        _numbering = numbering;
        _queryService = queryService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // === Template ===
    public byte[] GenerateTemplate(string moduleCode)
    {
        if (moduleCode != "materials")
            throw new DomainException("NOT_FOUND", $"Template for '{moduleCode}' not found", 404);

        using var wb = new XLWorkbook();
        var dataWs = wb.AddWorksheet("数据");
        var headers = new[] { "物料编码", "物料名称", "助记码", "批次管控", "标签类型", "默认单位", "默认每签数量" };
        for (var i = 0; i < headers.Length; i++)
            dataWs.Cell(1, i + 1).Value = headers[i];
        dataWs.Cell(2, 1).Value = "MAT-001";
        dataWs.Cell(2, 2).Value = "螺母 M6";
        dataWs.Cell(2, 3).Value = "LM";
        dataWs.Cell(2, 4).Value = "TRUE";
        dataWs.Cell(2, 5).Value = "SKU";
        dataWs.Cell(2, 6).Value = "CT";
        dataWs.Cell(2, 7).Value = "10.0000";

        var dictWs = wb.AddWorksheet("字典");
        dictWs.Cell(1, 1).Value = "字段";
        dictWs.Cell(1, 2).Value = "可选值";
        dictWs.Cell(1, 3).Value = "说明";
        dictWs.Cell(2, 1).Value = "标签类型";
        dictWs.Cell(2, 2).Value = "NONE, SKU, UNIQUE";
        dictWs.Cell(3, 1).Value = "默认单位";
        dictWs.Cell(3, 2).Value = "CT, PC, BOX, KG, G, L, M";

        var instWs = wb.AddWorksheet("说明");
        instWs.Cell(1, 1).Value = "导入说明";
        instWs.Cell(2, 1).Value = "1. 物料编码必填，1-64字符，不可与已有编码重复";
        instWs.Cell(3, 1).Value = "2. 物料名称必填，1-128字符";
        instWs.Cell(4, 1).Value = "3. 批次管控: TRUE/FALSE";
        instWs.Cell(5, 1).Value = "4. 标签类型: NONE/SKU/UNIQUE";
        instWs.Cell(6, 1).Value = "5. 默认单位: CT/PC/BOX/KG/G/L/M";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // === Precheck（只校验不落业务数据）===
    public async Task<ImportTaskResponse> PrecheckAsync(string moduleCode, byte[] fileBytes, string fileName, Guid? operatorId)
    {
        if (moduleCode != "materials")
            throw new DomainException("VALIDATION_ERROR", $"Module '{moduleCode}' not supported", 400);

        var taskNo = await _numbering.NextAsync("IMPORT_TASK");
        var task = new ImportTask
        {
            TaskNo = taskNo,
            ModuleCode = moduleCode,
            FileName = fileName,
            FileContent = Convert.ToBase64String(fileBytes),
            Direction = ImportTaskDirection.IMPORT,
            Status = ImportTaskStatus.PRECHECKING,
            OperatorId = operatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.ImportTasks.Add(task);
        await _db.SaveChangesAsync();

        var failures = new List<FailureDetail>();
        var total = 0;
        try
        {
            using var ms = new MemoryStream(fileBytes);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet(1);

            var existingCodes = await _db.Materials.Select(m => m.Code).ToHashSetAsync();
            var fileCodes = new HashSet<string>(StringComparer.Ordinal);

            var row = 2;
            while (!ws.Cell(row, 1).IsEmpty())
            {
                total++;
                var code = ws.Cell(row, 1).GetString().Trim();
                var name = ws.Cell(row, 2).GetString().Trim();
                var searchCode = ws.Cell(row, 3).GetString().Trim();
                var batchControlled = ws.Cell(row, 4).GetString().Trim().ToUpperInvariant();
                var labelType = ws.Cell(row, 5).GetString().Trim().ToUpperInvariant();
                var uom = ws.Cell(row, 6).GetString().Trim().ToUpperInvariant();
                var qtyPerLabel = ws.Cell(row, 7).GetString().Trim();

                if (string.IsNullOrEmpty(code))
                    failures.Add(new FailureDetail(row - 1, "code", "物料编码", code, "VALIDATION_ERROR", "物料编码必填"));
                else if (code.Length > 64)
                    failures.Add(new FailureDetail(row - 1, "code", "物料编码", code, "VALIDATION_ERROR", "物料编码超过64字符"));
                else if (!fileCodes.Add(code))
                    failures.Add(new FailureDetail(row - 1, "code", "物料编码", code, "MATERIAL_CODE_DUPLICATED", "文件内编码重复"));
                else if (existingCodes.Contains(code))
                    failures.Add(new FailureDetail(row - 1, "code", "物料编码", code, "MATERIAL_CODE_DUPLICATED", "物料编码已存在"));

                if (string.IsNullOrEmpty(name))
                    failures.Add(new FailureDetail(row - 1, "name", "物料名称", name, "VALIDATION_ERROR", "物料名称必填"));
                else if (name.Length > 128)
                    failures.Add(new FailureDetail(row - 1, "name", "物料名称", name, "VALIDATION_ERROR", "物料名称超过128字符"));

                if (!string.IsNullOrEmpty(searchCode) && searchCode.Length > 32)
                    failures.Add(new FailureDetail(row - 1, "searchCode", "助记码", searchCode, "VALIDATION_ERROR", "助记码超过32字符"));

                if (string.IsNullOrEmpty(labelType) || labelType is not ("NONE" or "SKU" or "UNIQUE"))
                    failures.Add(new FailureDetail(row - 1, "labelType", "标签类型", labelType, "VALIDATION_ERROR", "标签类型须为 NONE/SKU/UNIQUE"));

                if (string.IsNullOrEmpty(uom) || uom is not ("CT" or "PC" or "BOX" or "KG" or "G" or "L" or "M"))
                    failures.Add(new FailureDetail(row - 1, "defaultUom", "默认单位", uom, "VALIDATION_ERROR", "默认单位无效"));

                if (batchControlled is not "TRUE" and not "FALSE")
                    failures.Add(new FailureDetail(row - 1, "batchControlled", "批次管控", batchControlled, "VALIDATION_ERROR", "批次管控须为 TRUE/FALSE"));

                row++;
            }

            task.TotalCount = total;
            task.SuccessCount = total - failures.Count;
            task.FailCount = failures.Count;
            task.Status = ImportTaskStatus.PRECHECKED;

            foreach (var f in failures.Take(MaxInlineFailures))
            {
                _db.ImportTaskDetails.Add(new ImportTaskDetail
                {
                    ImportTaskId = task.Id,
                    RowNo = f.RowNo,
                    ColumnCode = f.ColumnCode,
                    ColumnName = f.ColumnName,
                    RawValue = f.RawValue,
                    ErrorCode = f.ErrorCode,
                    ErrorMsg = f.ErrorMsg
                });
            }
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            task.Status = ImportTaskStatus.FAILED;
            await _db.SaveChangesAsync();
            throw new DomainException("IMPORT_PARSE_ERROR", $"解析文件失败: {ex.Message}", 422);
        }

        return MapTaskResponse(task, failures.Take(MaxInlineFailures).ToList());
    }

    // === Execute（同一事务重校验 + 真实入库；全部通过才执行）===
    public async Task<ImportTaskResponse> ExecuteAsync(Guid taskId)
    {
        var task = await _db.ImportTasks.FirstOrDefaultAsync(t => t.Id == taskId)
            ?? throw new DomainException("IMPORT_TASK_NOT_FOUND", "导入任务不存在", 404);

        if (!task.CanExecute)
            throw new DomainException("IMPORT_VALIDATION_FAILED", "预校验未通过，无法执行导入", 422);
        if (string.IsNullOrEmpty(task.FileContent))
            throw new DomainException("IMPORT_VALIDATION_FAILED", "任务无文件数据", 422);

        task.Status = ImportTaskStatus.EXECUTING;
        await _db.SaveChangesAsync();

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var rolledBack = false;
        try
        {
            var fileBytes = Convert.FromBase64String(task.FileContent);
            using var ms = new MemoryStream(fileBytes);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheet(1);

            var existingCodes = await _db.Materials.Select(m => m.Code).ToHashSetAsync();
            var fileCodes = new HashSet<string>(StringComparer.Ordinal);
            var failures = new List<FailureDetail>();
            var total = 0;

            var row = 2;
            while (!ws.Cell(row, 1).IsEmpty())
            {
                total++;
                var code = ws.Cell(row, 1).GetString().Trim();
                var name = ws.Cell(row, 2).GetString().Trim();
                var searchCode = ws.Cell(row, 3).GetString().Trim();
                var batchControlled = ws.Cell(row, 4).GetString().Trim().ToUpperInvariant();
                var labelType = ws.Cell(row, 5).GetString().Trim().ToUpperInvariant();
                var uom = ws.Cell(row, 6).GetString().Trim().ToUpperInvariant();
                var qtyPerLabelStr = ws.Cell(row, 7).GetString().Trim();

                // 同事务重校验唯一性（防预校验-执行时间窗竞态，契约 C-8）
                if (string.IsNullOrEmpty(code) || code.Length > 64)
                    failures.Add(new FailureDetail(row - 1, "code", "物料编码", code, "VALIDATION_ERROR", "物料编码必填且不超过64字符"));
                else if (!fileCodes.Add(code))
                    failures.Add(new FailureDetail(row - 1, "code", "物料编码", code, "MATERIAL_CODE_DUPLICATED", "文件内编码重复"));
                else if (existingCodes.Contains(code))
                    failures.Add(new FailureDetail(row - 1, "code", "物料编码", code, "MATERIAL_CODE_DUPLICATED", "物料编码已存在（执行期竞态）"));

                if (string.IsNullOrEmpty(name) || name.Length > 128)
                    failures.Add(new FailureDetail(row - 1, "name", "物料名称", name, "VALIDATION_ERROR", "物料名称必填且不超过128字符"));
                if (labelType is not ("NONE" or "SKU" or "UNIQUE"))
                    failures.Add(new FailureDetail(row - 1, "labelType", "标签类型", labelType, "VALIDATION_ERROR", "标签类型须为 NONE/SKU/UNIQUE"));
                if (batchControlled is not "TRUE" and not "FALSE")
                    failures.Add(new FailureDetail(row - 1, "batchControlled", "批次管控", batchControlled, "VALIDATION_ERROR", "批次管控须为 TRUE/FALSE"));

                if (failures.Any(f => f.RowNo == row - 1))
                {
                    row++;
                    continue;
                }

                decimal? qtyPerLabel = null;
                if (!string.IsNullOrEmpty(qtyPerLabelStr))
                {
                    if (!decimal.TryParse(qtyPerLabelStr, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                    {
                        failures.Add(new FailureDetail(row - 1, "defaultQtyPerLabel", "默认每签数量", qtyPerLabelStr, "VALIDATION_ERROR", "默认每签数量必须为正数"));
                        row++;
                        continue;
                    }
                    qtyPerLabel = qty;
                }

                _db.Materials.Add(new Material
                {
                    Code = code,
                    Name = name,
                    SearchCode = string.IsNullOrEmpty(searchCode) ? null : searchCode,
                    BatchControlled = batchControlled == "TRUE",
                    LabelType = Enum.Parse<LabelType>(labelType),
                    DefaultUom = uom,
                    DefaultQtyPerLabel = qtyPerLabel,
                    Status = MaterialStatus.ENABLED,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                row++;
            }

            if (failures.Count > 0)
            {
                // 全部通过才可执行：任一失败 → 全回滚，不落任何业务数据
                await tx.RollbackAsync();
                rolledBack = true;
                task.Status = ImportTaskStatus.FAILED;
                task.CompletedAt = DateTime.UtcNow;
                task.TotalCount = total;
                task.SuccessCount = 0;
                task.FailCount = failures.Count;
                foreach (var f in failures.Take(MaxInlineFailures))
                {
                    _db.ImportTaskDetails.Add(new ImportTaskDetail
                    {
                        ImportTaskId = task.Id,
                        RowNo = f.RowNo,
                        ColumnCode = f.ColumnCode,
                        ColumnName = f.ColumnName,
                        RawValue = f.RawValue,
                        ErrorCode = f.ErrorCode,
                        ErrorMsg = f.ErrorMsg
                    });
                }
                await _db.SaveChangesAsync();
                throw new DomainException("IMPORT_VALIDATION_FAILED", "执行期重校验发现失败项，全部回滚", 422);
            }

            task.TotalCount = total;
            task.SuccessCount = total;
            task.FailCount = 0;
            task.Status = ImportTaskStatus.DONE;
            task.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DomainException)
        {
            if (!rolledBack)
                await tx.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            task.Status = ImportTaskStatus.FAILED;
            task.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            throw new DomainException("IMPORT_VALIDATION_FAILED", $"导入执行失败: {ex.Message}", 422);
        }

        return await GetTaskAsync(taskId) ?? MapTaskResponse(task);
    }

    // === Export（异步 PROCESSING→DONE；独立 DbContext 作用域）===
    public async Task<ImportTaskResponse> CreateExportAsync(string moduleCode, FilterRequest request, Guid? operatorId, string? operatorName)
    {
        if (moduleCode != "materials")
            throw new DomainException("VALIDATION_ERROR", $"Module '{moduleCode}' not supported for export", 400);

        var taskNo = await _numbering.NextAsync("IMPORT_TASK");
        var task = new ImportTask
        {
            TaskNo = taskNo,
            ModuleCode = moduleCode,
            FileName = $"{moduleCode}-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx",
            Direction = ImportTaskDirection.EXPORT,
            Status = ImportTaskStatus.PROCESSING,
            OperatorId = operatorId,
            OperatorName = operatorName,
            CreatedAt = DateTime.UtcNow
        };
        _db.ImportTasks.Add(task);
        await _db.SaveChangesAsync();
        var taskId = task.Id;

        // 后台任务：独立 scope + 独立 DbContext（复验意见：禁止捕获请求作用域）
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
                var queryService = scope.ServiceProvider.GetRequiredService<IQueryService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ImportExportService>>();

                var t = await db.ImportTasks.FirstOrDefaultAsync(x => x.Id == taskId)
                    ?? throw new InvalidOperationException($"Export task {taskId} not found");

                var source = db.Materials.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(request.Keyword))
                {
                    var kw = request.Keyword.ToLowerInvariant();
                    source = source.Where(m =>
                        m.Code.ToLower().Contains(kw) ||
                        m.Name.ToLower().Contains(kw) ||
                        (m.SearchCode != null && m.SearchCode.ToLower().Contains(kw)));
                }
                if (!string.IsNullOrWhiteSpace(request.Status))
                    source = source.Where(m => m.Status == Enum.Parse<MaterialStatus>(request.Status));

                // filter/sort/pageSize 全部生效（复验意见 B-06）
                var (_, result) = await queryService.ApplyAsync(
                    source, request, MaterialFields, MaterialSorts, "code", "asc");
                var materials = result.Items;

                using var wb = new XLWorkbook();
                var ws = wb.AddWorksheet("数据");
                var headers = new[] { "物料编码", "物料名称", "助记码", "批次管控", "标签类型", "默认单位", "默认每签数量", "状态" };
                for (var i = 0; i < headers.Length; i++)
                    ws.Cell(1, i + 1).Value = headers[i];

                var dataRow = 2;
                foreach (var m in materials)
                {
                    ws.Cell(dataRow, 1).Value = m.Code;
                    ws.Cell(dataRow, 2).Value = m.Name;
                    ws.Cell(dataRow, 3).Value = m.SearchCode ?? string.Empty;
                    ws.Cell(dataRow, 4).Value = m.BatchControlled ? "TRUE" : "FALSE";
                    ws.Cell(dataRow, 5).Value = m.LabelType.ToString();
                    ws.Cell(dataRow, 6).Value = m.DefaultUom;
                    ws.Cell(dataRow, 7).Value = m.DefaultQtyPerLabel?.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    ws.Cell(dataRow, 8).Value = m.Status.ToString();
                    dataRow++;
                }

                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                t.FileContent = Convert.ToBase64String(ms.ToArray());
                t.TotalCount = materials.Count;
                t.SuccessCount = materials.Count;
                t.FailCount = 0;
                t.Status = ImportTaskStatus.DONE;
                t.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
                    var t = await db.ImportTasks.FirstOrDefaultAsync(x => x.Id == taskId);
                    if (t != null)
                    {
                        t.Status = ImportTaskStatus.FAILED;
                        t.CompletedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                    _logger.LogError(ex, "导出任务 {TaskId} 执行失败", taskId);
                }
                catch (Exception inner)
                {
                    _logger.LogError(inner, "导出任务 {TaskId} 失败后状态更新也失败", taskId);
                }
            }
        });

        return MapTaskResponse(task);
    }

    public async Task<ImportTaskResponse?> GetTaskAsync(Guid id)
    {
        var t = await _db.ImportTasks.Include(x => x.Details).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return t == null ? null : MapTaskResponse(t, t.Details.OrderBy(d => d.RowNo).Select(ToFailureDetail).ToList());
    }

    public async Task<(byte[]? Data, string? FileName)> GetTaskFileAsync(Guid id)
    {
        var t = await _db.ImportTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("IMPORT_TASK_NOT_FOUND", "导入任务不存在", 404);
        if (t.Status != ImportTaskStatus.DONE || string.IsNullOrEmpty(t.FileContent))
            throw new DomainException("NOT_FOUND", "文件尚未生成", 404);

        return (Convert.FromBase64String(t.FileContent), t.FileName);
    }

    public async Task<(byte[]? Data, string? FileName)> GetFailReportAsync(Guid id)
    {
        var t = await _db.ImportTasks.Include(x => x.Details).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("IMPORT_TASK_NOT_FOUND", "导入任务不存在", 404);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("错误明细");
        var headers = new[] { "行号", "字段编码", "字段名称", "原始值", "错误码", "错误信息" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var row = 2;
        foreach (var d in t.Details.OrderBy(x => x.RowNo))
        {
            ws.Cell(row, 1).Value = d.RowNo;
            ws.Cell(row, 2).Value = d.ColumnCode ?? string.Empty;
            ws.Cell(row, 3).Value = d.ColumnName ?? string.Empty;
            ws.Cell(row, 4).Value = d.RawValue ?? string.Empty;
            ws.Cell(row, 5).Value = d.ErrorCode ?? string.Empty;
            ws.Cell(row, 6).Value = d.ErrorMsg ?? string.Empty;
            row++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), $"fail-report-{id}.xlsx");
    }

    private static ImportTaskResponse MapTaskResponse(ImportTask t, List<FailureDetail>? failures = null) => new(
        t.Id, t.TaskNo, t.ModuleCode, t.FileName,
        t.Direction.ToString(), t.Status.ToString(),
        t.TotalCount, t.SuccessCount, t.FailCount,
        t.CanExecute,
        t.FailReportUrl, t.FileUrl,
        t.OperatorId, t.OperatorName, t.CreatedAt,
        failures);

    private static FailureDetail ToFailureDetail(ImportTaskDetail d) =>
        new(d.RowNo, d.ColumnCode, d.ColumnName, d.RawValue, d.ErrorCode ?? "VALIDATION_ERROR", d.ErrorMsg ?? string.Empty);
}

