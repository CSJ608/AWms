using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            return await ParseExternalAsync(request.Content, ct);

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

    private async Task<ScanResult> ParseExternalAsync(string rawContent, CancellationToken ct)
    {
        var content = rawContent.Trim();
        if (TryParseGs1(content, out var gs1))
        {
            var material = await FindExternalMaterialAsync(gs1.MaterialCodes, ct);
            return ExternalResult(
                content,
                "GS1",
                material,
                gs1.Parsed,
                gs1.BatchProps,
                gs1.Quantity);
        }

        if (IsValidEan13(content))
        {
            var material = await FindExternalMaterialAsync(new[] { content }, ct);
            return ExternalResult(
                content,
                "EAN13",
                material,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["gtin"] = content },
                null,
                null);
        }

        var code128Content = content.StartsWith("]C0", StringComparison.Ordinal) ||
                             content.StartsWith("]C1", StringComparison.Ordinal)
            ? content[3..]
            : content;
        var code128Material = await FindExternalMaterialAsync(new[] { code128Content }, ct);
        if (code128Material != null || content.StartsWith("]C0", StringComparison.Ordinal))
        {
            return ExternalResult(
                content,
                "CODE128",
                code128Material,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = code128Content },
                null,
                null);
        }

        return Unknown();
    }

    private static ScanResult ExternalResult(
        string code,
        string format,
        Material? material,
        IReadOnlyDictionary<string, string> parsed,
        ScanBatchPropsItem? batchProps,
        string? quantity) =>
        new(
            "EXTERNAL_BARCODE",
            null,
            material == null ? null : MapMaterial(material),
            null,
            null,
            batchProps,
            quantity,
            null,
            null,
            new ScanExternalItem(code, format, parsed),
            Array.Empty<ScanWarning>());

    private async Task<Material?> FindExternalMaterialAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var candidates = codes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
        if (candidates.Count == 0)
            return null;
        return await _db.Materials.AsNoTracking().FirstOrDefaultAsync(x => candidates.Contains(x.Code), ct);
    }

    private static bool IsValidEan13(string value)
    {
        if (value.Length != 13 || value.Any(x => !char.IsAsciiDigit(x)))
            return false;
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = value[i] - '0';
            sum += i % 2 == 0 ? digit : digit * 3;
        }
        var checkDigit = (10 - sum % 10) % 10;
        return checkDigit == value[12] - '0';
    }

    private static bool TryParseGs1(string content, out Gs1ParseResult result)
    {
        var normalized = content.StartsWith("]C1", StringComparison.Ordinal) ? content[3..] : content;
        var values = normalized.Contains('(')
            ? ParseParenthesizedGs1(normalized)
            : ParseElementStringGs1(normalized);
        if (!values.ContainsKey("01") && values.Keys.All(x => x is not ("10" or "11" or "15" or "30")))
        {
            result = default!;
            return false;
        }

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        var materialCodes = new List<string>();
        if (values.TryGetValue("01", out var gtin))
        {
            if (gtin.Length != 14 || gtin.Any(x => !char.IsAsciiDigit(x)))
            {
                result = default!;
                return false;
            }
            parsed["gtin"] = gtin;
            materialCodes.Add(gtin);
            if (gtin[0] == '0')
                materialCodes.Add(gtin[1..]);
        }

        values.TryGetValue("10", out var lot);
        if (!string.IsNullOrWhiteSpace(lot))
            parsed["batchNo"] = lot;
        var productionDate = values.TryGetValue("11", out var production) ? ParseGs1Date(production) : null;
        var expiryDate = values.TryGetValue("15", out var expiry) ? ParseGs1Date(expiry) : null;
        if (productionDate != null)
            parsed["productionDate"] = productionDate;
        if (expiryDate != null)
            parsed["expiryDate"] = expiryDate;

        string? quantity = null;
        if (values.TryGetValue("30", out var count) &&
            decimal.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedQuantity) &&
            parsedQuantity > 0)
        {
            quantity = InboundOrderService.FormatQty(parsedQuantity);
            parsed["quantity"] = quantity;
        }

        result = new Gs1ParseResult(
            parsed,
            materialCodes,
            new ScanBatchPropsItem(lot, productionDate, expiryDate, null, null),
            quantity);
        return true;
    }

    private static Dictionary<string, string> ParseParenthesizedGs1(string content)
    {
        var matches = Regex.Matches(content, @"\((01|10|11|15|30)\)");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var start = match.Index + match.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
            values[match.Groups[1].Value] = content[start..end].Trim((char)29);
        }
        return values;
    }

    private static Dictionary<string, string> ParseElementStringGs1(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        while (index + 2 <= content.Length)
        {
            var ai = content.Substring(index, 2);
            index += 2;
            var fixedLength = ai switch
            {
                "01" => 14,
                "11" or "15" => 6,
                _ => 0
            };
            if (fixedLength > 0)
            {
                if (index + fixedLength > content.Length)
                    return new Dictionary<string, string>();
                values[ai] = content.Substring(index, fixedLength);
                index += fixedLength;
                continue;
            }
            if (ai is not ("10" or "30"))
                return new Dictionary<string, string>();

            var separator = content.IndexOf((char)29, index);
            if (separator < 0)
                separator = content.Length;
            values[ai] = content[index..separator];
            index = separator < content.Length ? separator + 1 : separator;
        }
        return values;
    }

    private static string? ParseGs1Date(string value)
    {
        if (value.Length != 6 || value.Any(x => !char.IsAsciiDigit(x)))
            return null;
        if (!DateOnly.TryParseExact(
                $"20{value}",
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return null;
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private sealed record Gs1ParseResult(
        IReadOnlyDictionary<string, string> Parsed,
        IReadOnlyList<string> MaterialCodes,
        ScanBatchPropsItem BatchProps,
        string? Quantity);

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
