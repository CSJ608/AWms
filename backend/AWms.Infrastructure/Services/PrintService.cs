using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Print;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AWms.Infrastructure.Services;

public class PrintService
{
    private readonly AWmsDbContext _db;
    private readonly NumberingService _numbering;
    private readonly string _root;

    public PrintService(AWmsDbContext db, NumberingService numbering, IConfiguration configuration)
    {
        _db = db;
        _numbering = numbering;
        _root = configuration["Storage:PrintRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "print-jobs");
    }

    public async Task<PrintJobDto> PrintInboundOrderQrAsync(InboundOrderQrPrintRequest request, Guid userId, string userName, CancellationToken ct = default)
    {
        var order = await _db.InboundOrders.Include(x => x.Warehouse).AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.InboundOrderId, ct)
            ?? throw new DomainException("ORDER_NOT_FOUND", "入库单不存在", 404);
        var content = AwmsCode(new { v = 1, t = "D", ty = order.Type.ToString(), d = order.OrderNo, wh = order.Warehouse.Code });
        var readable = $"单据：{order.OrderNo}\n类型：{order.Type}\n仓库：{order.Warehouse.Code}";
        var job = await CreateReadyJobAsync("INBOUND_ORDER", order.Id, "INBOUND_ORDER_QR", new[] { ("D", content, readable, (decimal?)null) }, userId, userName, createPdf: true, ct);
        return Map(job);
    }

    public async Task<PrintJobDto> PrintExternalLabelsAsync(ExternalLabelPrintRequest request, Guid userId, string userName, CancellationToken ct = default)
    {
        if (request.Items.Count is < 1 or > 100)
            throw new DomainException("VALIDATION_ERROR", "items 需 1-100 行", 400);

        var expanded = new List<(string LabelType, string Content, string Readable, decimal? Quantity)>();
        foreach (var item in request.Items)
        {
            if (item.Count is < 1 or > 1000)
                throw new DomainException("VALIDATION_ERROR", "count 范围 1-1000", 400);
            var material = await _db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.MaterialId, ct)
                ?? throw new DomainException("MATERIAL_NOT_FOUND", "物料不存在", 404);

            string? orderLineId = null;
            if (item.InboundOrderLineId.HasValue)
            {
                var line = await _db.InboundOrderLines
                    .Include(x => x.Order)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == item.InboundOrderLineId.Value, ct)
                    ?? throw new DomainException("ORDER_LINE_MISMATCH", "入库单行不存在", 400);
                if (line.MaterialId != item.MaterialId)
                    throw new DomainException("ORDER_LINE_MISMATCH", "入库单行物料不匹配", 400);
                await ValidateLabelSourceAsync(line.Order, item.Rt, item.Rc, ct);
                orderLineId = line.Id.ToString();
            }
            else if (!string.IsNullOrWhiteSpace(item.Rt) || !string.IsNullOrWhiteSpace(item.Rc))
            {
                await ValidateRtRcAsync(item.Rt, item.Rc, ct);
            }

            for (var i = 0; i < item.Count; i++)
            {
                var payload = new Dictionary<string, object?> { ["v"] = 1, ["t"] = "S", ["s"] = material.Code };
                if (orderLineId != null) payload["ol"] = orderLineId;
                if (!string.IsNullOrWhiteSpace(item.Rt)) payload["rt"] = item.Rt;
                if (!string.IsNullOrWhiteSpace(item.Rc)) payload["rc"] = item.Rc;
                expanded.Add(("S", AwmsCode(payload), $"物料：{material.Code} {material.Name}", null));
            }
        }

        var firstBiz = request.Items.Count == 1 && request.Items[0].InboundOrderLineId.HasValue
            ? ("INBOUND_ORDER_LINE", request.Items[0].InboundOrderLineId)
            : (null, (Guid?)null);
        var job = await CreateReadyJobAsync(firstBiz.Item1, firstBiz.Item2, "EXTERNAL_LABEL", expanded, userId, userName, createPdf: true, ct);
        return Map(job);
    }

    public async Task<PrintJobDto> PrintUniqueLabelsAsync(UniqueLabelsPrintRequest request, Guid userId, string userName, CancellationToken ct = default)
    {
        if (request.Count is < 1 or > 1000)
            throw new DomainException("VALIDATION_ERROR", "count 范围 1-1000", 400);
        var qtyPerCode = string.IsNullOrWhiteSpace(request.QtyPerCode) ? 1 : InboundOrderService.ParsePositiveDecimal(request.QtyPerCode, "qtyPerCode");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var line = await _db.InboundOrderLines
                .Include(x => x.Order)
                .Include(x => x.Material)
                .Include(x => x.UniqueCodes)
                .FirstOrDefaultAsync(x => x.Id == request.InboundOrderLineId, ct)
                ?? throw new DomainException("ORDER_LINE_MISMATCH", "入库单行不存在", 400);
            if (line.Order.Status is InboundOrderStatus.VOIDED or InboundOrderStatus.RECEIVED)
                throw new DomainException("ORDER_STATUS_INVALID", "当前入库单行不可生成唯一码", 409);

            var existingQty = line.UniqueCodes.Sum(x => x.Quantity);
            var addedQty = request.Count * qtyPerCode;
            if (line.Order.Type == InboundOrderType.PO && existingQty + addedQty > line.ExpectedQty)
                throw new DomainException("VALIDATION_ERROR", "PO 唯一码登记数量不得超过应到数量", 400);

            var codes = new List<string>(request.Count);
            for (var i = 0; i < request.Count; i++)
                codes.Add(await _numbering.NextAsyncCore("UNIQUE_CODE", "GLOBAL", tx));
            var items = new List<(string LabelType, string Content, string Readable, decimal? Quantity)>();
            foreach (var code in codes)
            {
                _db.UniqueCodes.Add(new UniqueCode
                {
                    OrderLineId = line.Id,
                    Code = code,
                    Quantity = qtyPerCode,
                    Status = UniqueCodeStatus.UNRECEIVED
                });
                var content = AwmsCode(new { v = 1, t = "U", s = line.Material.Code, u = code, q = InboundOrderService.FormatQty(qtyPerCode) });
                items.Add(("U", content, $"物料：{line.Material.Code} {line.Material.Name}\n唯一码：{code}\n数量：{InboundOrderService.FormatQty(qtyPerCode)}", qtyPerCode));
            }

            var job = await CreateReadyJobAsync("INBOUND_ORDER_LINE", line.Id, "UNIQUE_LABEL", items, userId, userName, createPdf: true, ct);
            await tx.CommitAsync(ct);
            return Map(job);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<PrintJobDto> PrintBatchLabelsAsync(BatchLabelsPrintRequest request, Guid userId, string userName, CancellationToken ct = default)
    {
        var line = await _db.ReceiptLines
            .Include(x => x.Material)
            .Include(x => x.Batch)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ReceiptLineId, ct)
            ?? throw new DomainException("RECEIPT_LINE_NOT_FOUND", "收货行不存在", 404);
        var qtyPerLabel = string.IsNullOrWhiteSpace(request.QtyPerLabel)
            ? line.Material.DefaultQtyPerLabel ?? line.ActualQty
            : InboundOrderService.ParsePositiveDecimal(request.QtyPerLabel, "qtyPerLabel");
        var items = SplitBatchLabels(line.Material.Code, line.Material.Name, line.Batch.BatchNo, line.ActualQty, qtyPerLabel).ToList();
        var job = await CreateReadyJobAsync("RECEIPT_LINE", line.Id, "BATCH_LABEL", items, userId, userName, createPdf: true, ct);
        return Map(job);
    }

    public async Task<PrintJobDto> PrintBatchLabelOneAsync(BatchLabelOnePrintRequest request, Guid userId, string userName, CancellationToken ct = default)
    {
        var quantity = InboundOrderService.ParsePositiveDecimal(request.Quantity, "quantity");
        var line = await _db.ReceiptLines.Include(x => x.Material).Include(x => x.Batch).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ReceiptLineId, ct)
            ?? throw new DomainException("RECEIPT_LINE_NOT_FOUND", "收货行不存在", 404);
        var content = AwmsCode(new { v = 1, t = "B", s = line.Material.Code, b = line.Batch.BatchNo, q = InboundOrderService.FormatQty(quantity) });
        var readable = $"物料：{line.Material.Code} {line.Material.Name}\n批次：{line.Batch.BatchNo}\n数量：{InboundOrderService.FormatQty(quantity)}";
        var job = await CreateReadyJobAsync("RECEIPT_LINE", line.Id, "BATCH_LABEL", new[] { ("B", content, readable, (decimal?)quantity) }, userId, userName, createPdf: false, ct);
        return Map(job);
    }

    public async Task<PrintJobDto> PrintReceiptAsync(Guid receiptId, Guid userId, string userName, CancellationToken ct = default)
    {
        var receipt = await _db.Receipts.Include(x => x.Warehouse).Include(x => x.Lines).ThenInclude(x => x.Material)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == receiptId, ct)
            ?? throw new DomainException("RECEIPT_NOT_FOUND", "收货单不存在", 404);
        var readable = new StringBuilder()
            .AppendLine($"收货单：{receipt.ReceiptNo}")
            .AppendLine($"仓库：{receipt.Warehouse.Code}")
            .AppendLine($"状态：{receipt.Status}");
        foreach (var line in receipt.Lines.OrderBy(x => x.LineNo))
            readable.AppendLine($"{line.LineNo}. {line.Material.Code} {InboundOrderService.FormatQty(line.ActualQty)}");
        var job = await CreateReadyJobAsync("RECEIPT", receipt.Id, "RECEIPT", new[] { ("R", receipt.ReceiptNo, readable.ToString(), (decimal?)null) }, userId, userName, createPdf: true, ct);
        return Map(job);
    }

    public async Task<PagedResult<PrintJobDto>> SearchJobsAsync(PrintJobSearchRequest request, CancellationToken ct = default)
    {
        if ((!string.IsNullOrWhiteSpace(request.BizType)) != request.BizId.HasValue)
            throw new DomainException("VALIDATION_ERROR", "bizType/bizId 必须成对", 400);

        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = request.PageSize is null or <= 0 ? 20 : request.PageSize.Value;
        var query = _db.PrintJobs.Include(x => x.Items).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.BizType))
            query = query.Where(x => x.BizType == request.BizType && x.BizId == request.BizId);
        if (!string.IsNullOrWhiteSpace(request.TemplateCode))
            query = query.Where(x => x.TemplateCode == request.TemplateCode);
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(x => x.Status == InboundOrderService.ParseEnum<PrintJobStatus>(request.Status, "status"));

        var total = await query.CountAsync(ct);
        var jobs = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<PrintJobDto>(jobs.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<PrintJobDto> GetJobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.PrintJobs.Include(x => x.Items).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("PRINT_JOB_NOT_FOUND", "打印作业不存在", 404);
        return Map(job);
    }

    public async Task<PrintJobDto> RetryAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.PrintJobs.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("PRINT_JOB_NOT_FOUND", "打印作业不存在", 404);
        if (job.Status != PrintJobStatus.FAILED)
            throw new DomainException("PRINT_JOB_STATUS_INVALID", "只有 FAILED 作业可重试", 409);

        job.Status = PrintJobStatus.READY;
        job.ErrorCode = null;
        job.ErrorMessage = null;
        job.FilePath = await WritePdfAsync(job.Id, job.Items.OrderBy(x => x.Seq).Select(x => x.ReadableText), ct);
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(job);
    }

    public async Task<(string Path, string FileName)> GetFileAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _db.PrintJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("PRINT_JOB_NOT_FOUND", "打印作业不存在", 404);
        if (job.Status == PrintJobStatus.FAILED)
            throw new DomainException(job.ErrorCode ?? "PRINT_GENERATION_FAILED", job.ErrorMessage ?? "打印生成失败", 500);
        if (job.Status != PrintJobStatus.READY || string.IsNullOrWhiteSpace(job.FilePath) || !File.Exists(job.FilePath))
            throw new DomainException("PRINT_JOB_NOT_READY", "打印文件尚未就绪", 409);
        return (job.FilePath, $"{job.TemplateCode}-{job.Id}.pdf");
    }

    private async Task<PrintJob> CreateReadyJobAsync(
        string? bizType,
        Guid? bizId,
        string templateCode,
        IEnumerable<(string LabelType, string Content, string Readable, decimal? Quantity)> items,
        Guid userId,
        string userName,
        bool createPdf,
        CancellationToken ct)
    {
        if ((bizType == null) != (bizId == null))
            throw new DomainException("VALIDATION_ERROR", "bizType/bizId 必须成对", 400);

        var itemList = items.ToList();
        if (itemList.Count == 0)
            throw new DomainException("VALIDATION_ERROR", "打印项目不能为空", 400);

        var job = new PrintJob
        {
            BizType = bizType,
            BizId = bizId,
            TemplateCode = templateCode,
            Status = PrintJobStatus.READY,
            CreatedBy = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var seq = 1;
        foreach (var item in itemList)
        {
            job.Items.Add(new PrintJobItem
            {
                Seq = seq++,
                LabelType = item.LabelType,
                Content = item.Content,
                ReadableText = item.Readable,
                Quantity = item.Quantity
            });
        }

        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        if (createPdf)
        {
            job.FilePath = await WritePdfAsync(job.Id, itemList.Select(x => x.Readable), ct);
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return job;
    }

    private async Task<string> WritePdfAsync(Guid jobId, IEnumerable<string> pages, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, $"{jobId:N}.pdf");
        var body = string.Join("\n\n---\n\n", pages).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var pdf = $"""
            %PDF-1.4
            1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
            2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
            3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj
            4 0 obj << /Length {body.Length + 64} >> stream
            BT /F1 12 Tf 40 790 Td ({body}) Tj ET
            endstream endobj
            5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj
            xref
            0 6
            0000000000 65535 f
            0000000010 00000 n
            0000000060 00000 n
            0000000117 00000 n
            0000000230 00000 n
            0000000360 00000 n
            trailer << /Root 1 0 R /Size 6 >>
            startxref
            460
            %%EOF
            """;
        await File.WriteAllTextAsync(path, pdf, Encoding.UTF8, ct);
        return path;
    }

    private static IEnumerable<(string LabelType, string Content, string Readable, decimal? Quantity)> SplitBatchLabels(string materialCode, string materialName, string batchNo, decimal actualQty, decimal qtyPerLabel)
    {
        var remaining = actualQty;
        while (remaining > 0)
        {
            var qty = Math.Min(remaining, qtyPerLabel);
            var content = AwmsCode(new { v = 1, t = "B", s = materialCode, b = batchNo, q = InboundOrderService.FormatQty(qty) });
            var readable = $"物料：{materialCode} {materialName}\n批次：{batchNo}\n数量：{InboundOrderService.FormatQty(qty)}";
            yield return ("B", content, readable, qty);
            remaining -= qty;
        }
    }

    private async Task ValidateLabelSourceAsync(InboundOrder order, string? rt, string? rc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rt) && string.IsNullOrWhiteSpace(rc))
            return;
        var (sourceType, sourceCode) = await ValidateRtRcAsync(rt, rc, ct);
        if (order.SourceType.HasValue &&
            (sourceType != order.SourceType.Value || !string.Equals(sourceCode, order.SourceCode, StringComparison.Ordinal)))
            throw new DomainException("SOURCE_MISMATCH", "标签来源与单据来源不一致", 400);
    }

    private async Task<(SourceType SourceType, string SourceCode)> ValidateRtRcAsync(string? rt, string? rc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rt) || string.IsNullOrWhiteSpace(rc))
            throw new DomainException("VALIDATION_ERROR", "rt/rc 必须成对", 400);
        var sourceType = rt.ToUpperInvariant() switch
        {
            "S" => SourceType.SUPPLIER,
            "W" => SourceType.WORKSHOP,
            _ => throw new DomainException("VALIDATION_ERROR", "rt 值无效", 400)
        };
        var source = await _db.Sources.AsNoTracking().FirstOrDefaultAsync(x => x.Type == sourceType && x.Code == rc, ct)
            ?? throw new DomainException("SOURCE_NOT_FOUND", "来源不存在", 404);
        if (source.Status != MaterialStatus.ENABLED)
            throw new DomainException("SOURCE_NOT_FOUND", "来源不可用", 404);
        return (sourceType, source.Code);
    }

    private static string AwmsCode(object payload) =>
        $"AWMS1:{JsonSerializer.Serialize(payload)}";

    private static PrintJobDto Map(PrintJob job) =>
        new(
            job.Id,
            job.BizType,
            job.BizId,
            job.TemplateCode,
            job.Status.ToString(),
            job.Items.OrderBy(x => x.Seq).Select(x =>
                new PrintJobItemDto(
                    x.LabelType,
                    x.Content,
                    x.ReadableText,
                    x.Quantity.HasValue ? InboundOrderService.FormatQty(x.Quantity.Value) : null)).ToList(),
            job.Status == PrintJobStatus.READY && !string.IsNullOrWhiteSpace(job.FilePath) ? $"/api/print/jobs/{job.Id}/file" : null,
            job.ErrorCode,
            job.CreatedBy,
            job.CreatedByName,
            job.CreatedAt,
            job.UpdatedAt);
}
