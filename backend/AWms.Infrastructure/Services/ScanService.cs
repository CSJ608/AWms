using System.Globalization;
using System.Text.Json;
using AWms.Domain.Dtos.Inbound;
using AWms.Domain.Dtos.Scan;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AWms.Infrastructure.Services;

public class ScanService
{
    private readonly AWmsDbContext _db;
    private readonly InboundOrderService _orders;

    public ScanService(AWmsDbContext db, InboundOrderService orders)
    {
        _db = db;
        _orders = orders;
    }

    public async Task<ScanResult> ParseAsync(ScanParseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new DomainException("VALIDATION_ERROR", "content 必填", 400);

        if (!request.Content.StartsWith("AWMS1:", StringComparison.Ordinal))
            return Unknown();

        using var doc = ParseAwmsJson(request.Content);
        var root = doc.RootElement;
        var type = ReadString(root, "t");
        return type switch
        {
            "D" => await ParseDocumentAsync(root, request.Context, ct),
            "S" => await ParseSkuAsync(root, request.Context, ct),
            "U" => await ParseUniqueAsync(root, request.Context, ct),
            "B" => await ParseBatchAsync(root, request.Context, ct),
            _ => Unknown()
        };
    }

    private async Task<ScanResult> ParseDocumentAsync(JsonElement root, ScanContext? context, CancellationToken ct)
    {
        var docType = ReadString(root, "ty");
        var docNo = ReadString(root, "d");
        if (string.IsNullOrWhiteSpace(docType) || string.IsNullOrWhiteSpace(docNo))
            throw new DomainException("SCAN_PARSE_ERROR", "单据码缺少 ty/d", 400);
        var type = InboundOrderService.ParseEnum<InboundOrderType>(docType, "ty");
        var order = await _db.InboundOrders
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.Material)
            .Include(x => x.Lines).ThenInclude(x => x.UniqueCodes)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Type == type && x.OrderNo == docNo, ct)
            ?? throw new DomainException("ORDER_NOT_FOUND", "入库单不存在", 404);

        var warnings = BuildDocumentWarnings(order, context);
        return new ScanResult(
            "DOCUMENT_QR",
            "D",
            null,
            null,
            null,
            null,
            null,
            await MapDocumentAsync(order, ct),
            null,
            null,
            warnings);
    }

    private async Task<ScanResult> ParseSkuAsync(JsonElement root, ScanContext? context, CancellationToken ct)
    {
        var material = await RequireMaterialByCodeAsync(ReadString(root, "s"), ct);
        var orderLineId = ReadGuid(root, "ol");
        ScanDocumentItem? document = null;
        var warnings = new List<ScanWarning>();
        if (orderLineId.HasValue)
        {
            var line = await _db.InboundOrderLines
                .Include(x => x.Order).ThenInclude(x => x.Warehouse)
                .Include(x => x.Order).ThenInclude(x => x.Lines).ThenInclude(x => x.Material)
                .Include(x => x.Order).ThenInclude(x => x.Lines).ThenInclude(x => x.UniqueCodes)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == orderLineId.Value, ct);
            if (line == null || line.MaterialId != material.Id)
                warnings.Add(new ScanWarning("ORDER_LINE_MISMATCH", "标签物料不在当前单据行", true));
            else
            {
                document = await MapDocumentAsync(line.Order, ct, onlyLineId: line.Id);
                warnings.AddRange(BuildDocumentWarnings(line.Order, context));
            }
        }

        var source = await MapSourceAsync(ReadString(root, "rt"), ReadString(root, "rc"), ct);
        if (context?.InboundOrderId is Guid orderId && source != null)
            warnings.AddRange(await BuildSourceWarningsAsync(orderId, source.SourceType, source.SourceCode, ct));

        return new ScanResult(
            "SKU_LABEL",
            "S",
            MapMaterial(material),
            null,
            null,
            new ScanBatchPropsItem(
                ReadString(root, "rb"),
                ReadString(root, "pd"),
                ReadString(root, "ex"),
                source?.SourceType,
                source?.SourceCode),
            null,
            document,
            source,
            null,
            warnings);
    }

    private async Task<ScanResult> ParseUniqueAsync(JsonElement root, ScanContext? context, CancellationToken ct)
    {
        var code = ReadString(root, "u");
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("SCAN_PARSE_ERROR", "唯一码缺少 u", 400);
        var unique = await _db.UniqueCodes
            .Include(x => x.OrderLine).ThenInclude(x => x.Material)
            .Include(x => x.OrderLine).ThenInclude(x => x.Order)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, ct);
        if (unique == null)
            return Unknown();

        var warnings = new List<ScanWarning>();
        if (context?.InboundOrderId is Guid orderId && unique.OrderLine.OrderId != orderId)
            warnings.Add(new ScanWarning("UNIQUE_CODE_NOT_IN_ORDER", "唯一码不在当前入库单", true));
        if (unique.Status == UniqueCodeStatus.RECEIVED)
            warnings.Add(new ScanWarning("UNIQUE_CODE_ALREADY_RECEIVED", "唯一码已收货", true));

        return new ScanResult(
            "UNIQUE_LABEL",
            "U",
            MapMaterial(unique.OrderLine.Material),
            new ScanUniqueCodeItem(unique.Code, unique.Status.ToString(), InboundOrderService.FormatQty(unique.Quantity), unique.ReceivedAt),
            null,
            null,
            InboundOrderService.FormatQty(unique.Quantity),
            null,
            null,
            null,
            warnings);
    }

    private async Task<ScanResult> ParseBatchAsync(JsonElement root, ScanContext? context, CancellationToken ct)
    {
        var material = await RequireMaterialByCodeAsync(ReadString(root, "s"), ct);
        var batchNo = ReadString(root, "b");
        if (string.IsNullOrWhiteSpace(batchNo))
            throw new DomainException("SCAN_PARSE_ERROR", "批次码缺少 b", 400);
        var batch = await _db.Batches.AsNoTracking().FirstOrDefaultAsync(x => x.MaterialId == material.Id && x.BatchNo == batchNo, ct)
            ?? throw new DomainException("BATCH_NOT_FOUND", "批次不存在", 404);

        return new ScanResult(
            "BATCH_LABEL",
            "B",
            MapMaterial(material),
            null,
            new ScanBatchItem(
                batch.Id,
                batch.BatchNo,
                batch.SourceBatchNo,
                batch.ProductionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                batch.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            null,
            ReadString(root, "q"),
            null,
            null,
            null,
            Array.Empty<ScanWarning>());
    }

    private async Task<ScanDocumentItem> MapDocumentAsync(InboundOrder order, CancellationToken ct, Guid? onlyLineId = null)
    {
        var item = (await _orders.MapOrdersAsync(new[] { order }, ct))[0];
        var lines = item.Lines
            .Where(x => onlyLineId == null || x.Id == onlyLineId.Value)
            .Select(x => new ScanDocumentLineItem(
                x.Id,
                x.LineNo,
                x.MaterialId,
                x.MaterialCode,
                x.MaterialName,
                x.ExpectedQty,
                x.ReceivedQty,
                x.RemainingQty,
                x.UniqueCodes))
            .ToList();
        return new ScanDocumentItem(item.Id, item.Type, item.OrderNo, item.WarehouseId, item.WarehouseCode, item.Status, lines);
    }

    private static List<ScanWarning> BuildDocumentWarnings(InboundOrder order, ScanContext? context)
    {
        var warnings = new List<ScanWarning>();
        if (order.Status == InboundOrderStatus.VOIDED)
            warnings.Add(new ScanWarning("ORDER_VOIDED", "入库单已作废", true));
        else if (order.Status == InboundOrderStatus.RECEIVED)
            warnings.Add(new ScanWarning("ORDER_STATUS_INVALID", "入库单已收完", true));
        if (context?.WarehouseId is Guid warehouseId && order.WarehouseId != warehouseId)
            warnings.Add(new ScanWarning("WAREHOUSE_MISMATCH", "单据仓库与当前仓库不一致", true));
        return warnings;
    }

    private async Task<List<ScanWarning>> BuildSourceWarningsAsync(Guid orderId, string sourceType, string sourceCode, CancellationToken ct)
    {
        var order = await _db.InboundOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order?.SourceType == null)
            return new List<ScanWarning>();
        return order.SourceType.ToString() == sourceType && order.SourceCode == sourceCode
            ? new List<ScanWarning>()
            : new List<ScanWarning> { new("SOURCE_MISMATCH", "标签来源与当前单据来源不一致", true) };
    }

    private async Task<Material> RequireMaterialByCodeAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("SCAN_PARSE_ERROR", "标签缺少物料编码", 400);
        return await _db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct)
            ?? throw new DomainException("MATERIAL_NOT_FOUND", "物料不存在", 404);
    }

    private async Task<ScanSourceItem?> MapSourceAsync(string? rt, string? rc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rt) && string.IsNullOrWhiteSpace(rc))
            return null;
        if (string.IsNullOrWhiteSpace(rt) || string.IsNullOrWhiteSpace(rc))
            throw new DomainException("SCAN_PARSE_ERROR", "来源 rt/rc 必须成对", 400);
        var sourceType = rt.ToUpperInvariant() switch
        {
            "S" => SourceType.SUPPLIER,
            "W" => SourceType.WORKSHOP,
            _ => throw new DomainException("SCAN_PARSE_ERROR", "来源 rt 无效", 400)
        };
        var source = await _db.Sources.AsNoTracking().FirstOrDefaultAsync(x => x.Type == sourceType && x.Code == rc, ct);
        return source == null ? null : new ScanSourceItem(sourceType.ToString(), source.Code, source.Name);
    }

    private static ScanMaterialItem MapMaterial(Material material) =>
        new(
            material.Id,
            material.Code,
            material.Name,
            material.BatchControlled,
            material.LabelType.ToString(),
            material.DefaultUom,
            material.DefaultQtyPerLabel.HasValue ? InboundOrderService.FormatQty(material.DefaultQtyPerLabel.Value) : null);

    private static JsonDocument ParseAwmsJson(string content)
    {
        try
        {
            return JsonDocument.Parse(content["AWMS1:".Length..]);
        }
        catch (JsonException ex)
        {
            throw new DomainException("SCAN_PARSE_ERROR", $"标签内容损坏：{ex.Message}", 400);
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
            return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static Guid? ReadGuid(JsonElement element, string name) =>
        Guid.TryParse(ReadString(element, name), out var id) ? id : null;

    private static ScanResult Unknown() =>
        new("UNKNOWN", null, null, null, null, null, null, null, null, null, Array.Empty<ScanWarning>(), "未识别，请手动输入");
}
