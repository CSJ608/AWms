using System.Globalization;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Inbound;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AWms.Infrastructure.Services;

public class InboundOrderService
{
    private readonly AWmsDbContext _db;
    private readonly IQueryService _queryService;
    private readonly NumberingService _numbering;

    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "orderNo", "type", "warehouseId", "sourceType", "sourceCode", "status", "createdAt", "updatedAt"
    };

    private static readonly HashSet<string> Sorts = new(StringComparer.Ordinal)
    {
        "orderNo", "type", "status", "createdAt"
    };

    public InboundOrderService(AWmsDbContext db, IQueryService queryService, NumberingService numbering)
    {
        _db = db;
        _queryService = queryService;
        _numbering = numbering;
    }

    public async Task<PagedResult<InboundOrderItem>> SearchAsync(InboundOrderSearchRequest request, CancellationToken ct = default)
    {
        var query = _db.InboundOrders
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.Material)
            .Include(x => x.Lines).ThenInclude(x => x.UniqueCodes)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.OrderNo))
            query = query.Where(x => x.OrderNo.ToLower().Contains(request.OrderNo.ToLower()));
        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(x => x.Type == ParseEnum<InboundOrderType>(request.Type, "type"));
        if (request.WarehouseId.HasValue)
            query = query.Where(x => x.WarehouseId == request.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(x => x.Status == ParseEnum<InboundOrderStatus>(request.Status, "status"));

        var filterRequest = new FilterRequest(
            null, null, null, request.Status, request.Type, null, null, null,
            request.Sort?.ToList(), request.Filter, request.Page, request.PageSize);
        var (_, result) = await _queryService.ApplyAsync(query, filterRequest, Fields, Sorts, "createdAt", "desc", isTimeBasedList: true);
        var mapped = await MapOrdersAsync(result.Items, ct);
        return new PagedResult<InboundOrderItem>(mapped, result.Total, result.Page, result.PageSize);
    }

    public async Task<InboundOrderItem> GetAsync(Guid id, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(id, asNoTracking: true, ct)
            ?? throw new DomainException("ORDER_NOT_FOUND", "入库单不存在", 404);
        return (await MapOrdersAsync(new[] { order }, ct))[0];
    }

    public async Task<InboundOrderItem> CreateAsync(CreateInboundOrderRequest request, string operatorName, CancellationToken ct = default)
    {
        var type = ParseEnum<InboundOrderType>(request.Type, "type");
        if (request.Lines.Count == 0)
            throw new DomainException("VALIDATION_ERROR", "lines 至少一行", 400);

        var warehouse = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.WarehouseId, ct)
            ?? throw new DomainException("WAREHOUSE_NOT_FOUND", "仓库不存在", 404);
        if (warehouse.Status != MaterialStatus.ENABLED)
            throw new DomainException("WAREHOUSE_NOT_FOUND", "仓库不可用", 404);

        var (sourceType, sourceCode) = await ValidateSourceForOrderAsync(type, request.SourceType, request.SourceCode, ct);
        var materialIds = request.Lines.Select(x => x.MaterialId).Distinct().ToList();
        var materials = await _db.Materials.Where(x => materialIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (materials.Count != materialIds.Count)
            throw new DomainException("MATERIAL_NOT_FOUND", "物料不存在", 404);
        if (materials.Values.Any(x => x.Status != MaterialStatus.ENABLED))
            throw new DomainException("MATERIAL_NOT_FOUND", "物料不可用", 404);

        var order = new InboundOrder
        {
            OrderNo = await _numbering.NextAsync("INBOUND_ORDER", type.ToString()),
            Type = type,
            WarehouseId = warehouse.Id,
            SourceType = sourceType,
            SourceCode = sourceCode,
            Status = InboundOrderStatus.CONFIRMED,
            CreatedBy = operatorName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var lineNo = 1;
        foreach (var line in request.Lines)
        {
            var qty = ParsePositiveDecimal(line.ExpectedQty, "expectedQty");
            order.Lines.Add(new InboundOrderLine
            {
                LineNo = lineNo++,
                MaterialId = line.MaterialId,
                ExpectedQty = qty
            });
        }

        _db.InboundOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(order.Id, ct);
    }

    public async Task<InboundOrderItem> VoidAsync(Guid id, string reason, string operatorName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("VALIDATION_ERROR", "reason 必填", 400);

        var order = await LoadOrderAsync(id, asNoTracking: false, ct)
            ?? throw new DomainException("ORDER_NOT_FOUND", "入库单不存在", 404);

        if (order.Status is not (InboundOrderStatus.CONFIRMED or InboundOrderStatus.RECEIVING))
            throw new DomainException("ORDER_STATUS_INVALID", "当前状态不可作废", 409);

        order.Status = InboundOrderStatus.VOIDED;
        order.VoidedAt = DateTime.UtcNow;
        order.VoidedBy = operatorName;
        order.VoidReason = reason;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    internal async Task<InboundOrder?> LoadOrderAsync(Guid id, bool asNoTracking, CancellationToken ct = default)
    {
        var query = _db.InboundOrders
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.Material)
            .Include(x => x.Lines).ThenInclude(x => x.UniqueCodes)
            .AsQueryable();
        if (asNoTracking)
            query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    internal async Task<IReadOnlyList<InboundOrderItem>> MapOrdersAsync(IEnumerable<InboundOrder> orders, CancellationToken ct = default)
    {
        var list = orders.ToList();
        var lineIds = list.SelectMany(x => x.Lines).Select(x => x.Id).ToList();
        var received = await _db.ReceiptLines.AsNoTracking()
            .Where(x => x.OrderLineId != null && lineIds.Contains(x.OrderLineId.Value))
            .GroupBy(x => x.OrderLineId!.Value)
            .Select(x => new { OrderLineId = x.Key, Qty = x.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => x.OrderLineId, x => x.Qty, ct);

        return list.Select(order => new InboundOrderItem(
            order.Id,
            order.OrderNo,
            order.Type.ToString(),
            order.WarehouseId,
            order.Warehouse.Code,
            order.SourceType?.ToString(),
            order.SourceCode,
            order.Status.ToString(),
            order.Lines.OrderBy(x => x.LineNo).Select(line =>
            {
                var receivedQty = received.GetValueOrDefault(line.Id);
                var remaining = Math.Max(line.ExpectedQty - receivedQty, 0);
                return new InboundOrderLineItem(
                    line.Id,
                    line.LineNo,
                    line.MaterialId,
                    line.Material.Code,
                    line.Material.Name,
                    FormatQty(line.ExpectedQty),
                    FormatQty(receivedQty),
                    FormatQty(remaining),
                    line.UniqueCodes.OrderBy(x => x.Code).Select(x =>
                        new UniqueCodeItem(x.Code, FormatQty(x.Quantity), x.Status.ToString(), x.ReceivedAt)).ToList());
            }).ToList(),
            order.CreatedAt,
            order.CreatedBy,
            order.VoidedAt,
            order.VoidedBy,
            order.VoidReason)).ToList();
    }

    private async Task<(SourceType? SourceType, string? SourceCode)> ValidateSourceForOrderAsync(
        InboundOrderType type,
        string? sourceTypeValue,
        string? sourceCode,
        CancellationToken ct)
    {
        if (type == InboundOrderType.PO)
            return (SourceType.SUPPLIER, await RequireEnabledSourceAsync(SourceType.SUPPLIER, sourceTypeValue, sourceCode, ct));
        if (type == InboundOrderType.PR)
            return (SourceType.WORKSHOP, await RequireEnabledSourceAsync(SourceType.WORKSHOP, sourceTypeValue, sourceCode, ct));

        if (string.IsNullOrWhiteSpace(sourceTypeValue) && string.IsNullOrWhiteSpace(sourceCode))
            return (null, null);
        if (string.IsNullOrWhiteSpace(sourceTypeValue) || string.IsNullOrWhiteSpace(sourceCode))
            throw new DomainException("VALIDATION_ERROR", "sourceType/sourceCode 必须同时为空或同时有值", 400);
        var parsed = ParseEnum<SourceType>(sourceTypeValue, "sourceType");
        return (parsed, await RequireEnabledSourceAsync(parsed, parsed.ToString(), sourceCode, ct));
    }

    private async Task<string> RequireEnabledSourceAsync(SourceType expected, string? sourceTypeValue, string? sourceCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new DomainException("VALIDATION_ERROR", "sourceCode 必填", 400);
        if (string.IsNullOrWhiteSpace(sourceTypeValue) || !Enum.TryParse<SourceType>(sourceTypeValue, true, out var parsed) || parsed != expected)
            throw new DomainException("VALIDATION_ERROR", $"sourceType 必须为 {expected}", 400);

        var source = await _db.Sources.AsNoTracking().FirstOrDefaultAsync(x => x.Type == expected && x.Code == sourceCode, ct)
            ?? throw new DomainException("SOURCE_NOT_FOUND", "来源不存在", 404);
        if (source.Status != MaterialStatus.ENABLED)
            throw new DomainException("SOURCE_NOT_FOUND", "来源不可用", 404);
        return source.Code;
    }

    internal static TEnum ParseEnum<TEnum>(string? value, string fieldName) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value) || !Enum.TryParse<TEnum>(value, true, out var result))
            throw new DomainException("VALIDATION_ERROR", $"{fieldName} 值无效：{value}", 400);
        return result;
    }

    internal static decimal ParsePositiveDecimal(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ||
            result <= 0)
            throw new DomainException("VALIDATION_ERROR", $"{fieldName} 必须为正数", 400);
        return decimal.Round(result, 4);
    }

    internal static string FormatQty(decimal value) => value.ToString("F4", CultureInfo.InvariantCulture);
}
