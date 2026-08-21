using System.Data;
using System.Globalization;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Receipts;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AWms.Infrastructure.Services;

public class ReceiptService
{
    private readonly AWmsDbContext _db;
    private readonly NumberingService _numbering;
    private readonly AttachmentService _attachments;
    private readonly IQueryService _queryService;

    private static readonly HashSet<string> ReceiptFilterFields = new(StringComparer.Ordinal)
    {
        "receiptNo", "warehouseId", "inboundOrderId", "sourceDocType", "sourceDocNo", "status", "operatorId", "occurredAt"
    };

    private static readonly HashSet<string> ReceiptSortFields = new(StringComparer.Ordinal)
    {
        "receiptNo", "status", "occurredAt"
    };

    public ReceiptService(
        AWmsDbContext db,
        NumberingService numbering,
        AttachmentService attachments,
        IQueryService queryService)
    {
        _db = db;
        _numbering = numbering;
        _attachments = attachments;
        _queryService = queryService;
    }

    public async Task<ReceiptItem> SubmitAsync(SubmitReceiptRequest request, Guid operatorId, string operatorName, CancellationToken ct = default)
    {
        if (request.Lines.Count == 0)
            throw new DomainException("VALIDATION_ERROR", "lines 至少一行", 400);
        if (request.Photos is { Count: > 3 })
            throw new DomainException("VALIDATION_ERROR", "收货照片最多 3 张", 400);

        await using var tx = await BusinessTransaction.BeginAsync(_db, IsolationLevel.ReadCommitted, ct);
        try
        {
            var warehouse = await RequireWarehouseAsync(request.WarehouseId, ct);
            var staging = await RequireStagingLocationAsync(request.WarehouseId, request.StagingLocationId, ct);

            InboundOrder? order = null;
            InboundOrderType sourceDocType;
            string? sourceDocNo;
            SourceType? headerSourceType;
            string? headerSourceCode;

            if (request.InboundOrderId.HasValue)
            {
                await LockOrderAsync(request.InboundOrderId.Value, ct);
                order = await LoadOrderForWriteAsync(request.InboundOrderId.Value, ct)
                    ?? throw new DomainException("ORDER_NOT_FOUND", "入库单不存在", 404);

                if (order.Status == InboundOrderStatus.VOIDED)
                    throw new DomainException("ORDER_VOIDED", "入库单已作废", 409);
                if (order.Status == InboundOrderStatus.RECEIVED)
                    throw new DomainException("ORDER_STATUS_INVALID", "入库单已收完", 409);
                if (order.WarehouseId != request.WarehouseId)
                    throw new DomainException("WAREHOUSE_MISMATCH", "收货仓库与入库单不一致", 400);

                sourceDocType = order.Type;
                sourceDocNo = order.OrderNo;
                headerSourceType = order.SourceType;
                headerSourceCode = order.SourceCode;
            }
            else
            {
                sourceDocType = InboundOrderService.ParseEnum<InboundOrderType>(request.SourceDocType, "sourceDocType");
                if (sourceDocType == InboundOrderType.PO)
                    throw new DomainException("ORDER_REQUIRED_FOR_PO", "采购收货必须引用预建入库单", 400);
                sourceDocNo = request.SourceDocNo;
                (headerSourceType, headerSourceCode) = await ValidateReceiptHeaderSourceAsync(sourceDocType, request.SourceType, request.SourceCode, ct);
            }

            var receipt = new Receipt
            {
                ReceiptNo = await _numbering.NextAsyncCore("RECEIPT", "GLOBAL", tx.Transaction),
                WarehouseId = warehouse.Id,
                StagingLocationId = staging.Id,
                InboundOrderId = order?.Id,
                SourceDocType = sourceDocType,
                SourceDocNo = sourceDocNo,
                SourceType = headerSourceType,
                SourceCode = headerSourceCode,
                Status = ReceiptStatus.RECEIVING,
                OperatorId = operatorId,
                OperatorName = operatorName,
                OccurredAt = DateTime.UtcNow
            };
            _db.Receipts.Add(receipt);

            var resolvedOrderLines = new List<InboundOrderLine?>(request.Lines.Count);
            var referencedOrderLineIds = new HashSet<Guid>();
            foreach (var lineRequest in request.Lines)
            {
                var resolved = ResolveOrderLine(order, lineRequest, lineRequest.MaterialId);
                if (resolved != null && !referencedOrderLineIds.Add(resolved.Id))
                    throw new DomainException("ORDER_LINE_MISMATCH", "同一收货请求不得重复引用同一入库单行", 400);
                resolvedOrderLines.Add(resolved);
            }

            var prepared = new List<ReceiptLine>();
            var lineNo = 1;
            for (var requestIndex = 0; requestIndex < request.Lines.Count; requestIndex++)
            {
                var lineRequest = request.Lines[requestIndex];
                var material = await RequireMaterialAsync(lineRequest.MaterialId, ct);
                var quantity = InboundOrderService.ParsePositiveDecimal(lineRequest.Quantity, "quantity");
                var orderLine = resolvedOrderLines[requestIndex];
                await ValidateOrderLineQuantityAsync(order, orderLine, quantity, ct);
                await ValidateUniqueCodesAsync(order, orderLine, material, lineRequest.UniqueCodes, quantity, ct);

                var batch = await ResolveBatchAsync(sourceDocType, material, lineRequest.BatchId, lineRequest.BatchProps, headerSourceType, headerSourceCode, tx.Transaction, ct);

                prepared.Add(new ReceiptLine
                {
                    Receipt = receipt,
                    LineNo = lineNo++,
                    OrderLineId = orderLine?.Id,
                    OrderLineNo = orderLine?.LineNo,
                    MaterialId = material.Id,
                    BatchId = batch.Id,
                    ExpectedQty = orderLine?.ExpectedQty,
                    ActualQty = quantity,
                    QtyDiff = orderLine == null ? null : quantity - orderLine.ExpectedQty,
                    Status = ReceiptLineStatus.RECEIVED,
                    SourceBatchNo = batch.SourceBatchNo,
                    ProductionDate = batch.ProductionDate,
                    ExpiryDate = batch.ExpiryDate,
                    ReceivedAt = receipt.OccurredAt
                });
            }

            _db.ReceiptLines.AddRange(prepared);
            await _db.SaveChangesAsync(ct);

            foreach (var line in prepared)
            {
                await AddInventoryAsync(
                    receipt.WarehouseId,
                    line.MaterialId,
                    line.BatchId,
                    StockSubjectStatus.PENDING_INSPECTION,
                    receipt.StagingLocationId,
                    line.ActualQty,
                    TxnGroupType.RECEIPT,
                    LedgerReason.RECEIPT,
                    receipt.SourceDocType.ToString(),
                    receipt.ReceiptNo,
                    operatorId,
                    tx.Transaction,
                    ct);
            }

            await MarkUniqueCodesReceivedAsync(request.Lines, receipt.OccurredAt, ct);
            if (request.Photos is { Count: > 0 })
                await _attachments.ClaimAsync(request.Photos, "RECEIPT", receipt.Id, operatorId, ct);

            if (order != null)
                await AdvanceInboundOrderStatusAsync(order, ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await GetAsync(receipt.Id, ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PagedResult<ReceiptItem>> SearchAsync(ReceiptSearchRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = request.PageSize is null or <= 0 ? 20 : request.PageSize.Value;
        var query = ReceiptQuery().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(x => x.Status == InboundOrderService.ParseEnum<ReceiptStatus>(request.Status, "status"));
        if (request.WarehouseId.HasValue)
            query = query.Where(x => x.WarehouseId == request.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(request.ReceiptNo))
            query = query.Where(x => x.ReceiptNo.ToLower().Contains(request.ReceiptNo.ToLower()));
        if (request.DateFrom.HasValue)
            query = query.Where(x => x.OccurredAt >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(x => x.OccurredAt < request.DateTo.Value);

        var filterRequest = new FilterRequest(
            null, null, null, null, null, null, null, null,
            request.Sort?.ToList(), request.Filter, page, pageSize);
        var (_, result) = await _queryService.ApplyAsync(
            query,
            filterRequest,
            ReceiptFilterFields,
            ReceiptSortFields,
            "occurredAt",
            "desc",
            isTimeBasedList: true);
        var items = await MapReceiptsAsync(result.Items, ct);
        return new PagedResult<ReceiptItem>(items, result.Total, result.Page, result.PageSize);
    }

    public async Task<ReceiptItem> GetAsync(Guid id, CancellationToken ct = default)
    {
        var receipt = await ReceiptQuery().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("RECEIPT_NOT_FOUND", "收货单不存在", 404);
        return (await MapReceiptsAsync(new[] { receipt }, ct))[0];
    }

    public async Task<PagedResult<QualityTodoItem>> SearchQualityTodosAsync(QualityTodoSearchRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = request.PageSize is null or <= 0 ? 20 : request.PageSize.Value;
        var query = _db.ReceiptLines
            .Include(x => x.Receipt).ThenInclude(x => x.Warehouse)
            .Include(x => x.Material)
            .Include(x => x.Batch)
            .AsNoTracking()
            .Where(x => x.Status == ReceiptLineStatus.RECEIVED)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(x => x.Receipt.WarehouseId == request.WarehouseId.Value);
        if (request.MaterialId.HasValue)
            query = query.Where(x => x.MaterialId == request.MaterialId.Value);
        if (request.BatchId.HasValue)
            query = query.Where(x => x.BatchId == request.BatchId.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLower();
            query = query.Where(x => x.Receipt.ReceiptNo.ToLower().Contains(kw) ||
                                     x.Material.Code.ToLower().Contains(kw) ||
                                     x.Material.Name.ToLower().Contains(kw) ||
                                     x.Batch.BatchNo.ToLower().Contains(kw));
        }

        var total = await query.CountAsync(ct);
        var lines = await query.OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<QualityTodoItem>(lines.Select(MapQualityTodo).ToList(), total, page, pageSize);
    }

    public async Task<ReceiptItem> SubmitQualityCheckAsync(Guid receiptLineId, QualityCheckRequest request, Guid operatorId, string operatorName, CancellationToken ct = default)
    {
        await using var tx = await BusinessTransaction.BeginAsync(_db, IsolationLevel.ReadCommitted, ct);
        try
        {
            await LockReceiptLineAsync(receiptLineId, ct);
            var line = await LoadReceiptLineForWriteAsync(receiptLineId, ct)
                ?? throw new DomainException("RECEIPT_LINE_NOT_FOUND", "收货行不存在", 404);
            if (line.Status != ReceiptLineStatus.RECEIVED)
                throw new DomainException("QC_STATUS_INVALID", "当前行不可质检", 409);
            if (await _db.QualityChecks.AnyAsync(x => x.ReceiptLineId == receiptLineId, ct))
                throw new DomainException("QC_STATUS_INVALID", "当前行已质检", 409);

            var checkedQty = InboundOrderService.ParsePositiveDecimal(request.CheckedQty, "checkedQty");
            if (checkedQty != line.ActualQty)
                throw new DomainException("VALIDATION_ERROR", "checkedQty 必须等于收货行数量", 400);

            var result = InboundOrderService.ParseEnum<QualityCheckResult>(request.Result, "result");
            var photoIds = request.PhotoIds?.Distinct().ToList() ?? new List<Guid>();
            var check = new QualityCheck
            {
                ReceiptLineId = line.Id,
                CheckedQty = checkedQty,
                Result = result,
                OperatorId = operatorId,
                OperatorName = operatorName,
                CheckedAt = DateTime.UtcNow,
                Note = request.Note,
                PhotoIdsJson = JsonSerializer.Serialize(photoIds)
            };

            if (result == QualityCheckResult.PASS)
            {
                if (photoIds.Count > 0)
                    throw new DomainException("VALIDATION_ERROR", "PASS 不接收照片", 400);
                line.Status = ReceiptLineStatus.CHECKED;
                await MoveInventoryAsync(
                    line.Receipt.WarehouseId,
                    line.MaterialId,
                    line.BatchId,
                    line.Receipt.StagingLocationId,
                    line.Receipt.StagingLocationId,
                    StockSubjectStatus.PENDING_INSPECTION,
                    StockSubjectStatus.AVAILABLE,
                    line.ActualQty,
                    TxnGroupType.QUALITY_CHECK,
                    LedgerReason.QUALITY_CHECK,
                    line.Receipt.SourceDocType.ToString(),
                    line.Receipt.ReceiptNo,
                    operatorId,
                    null,
                    tx.Transaction,
                    ct);
            }
            else
            {
                if (photoIds.Count is < 1 or > 3)
                    throw new DomainException("VALIDATION_ERROR", "异常照片需 1-3 张", 400);
                check.ExceptionReason = InboundOrderService.ParseEnum<QualityExceptionReason>(request.ExceptionReason, "exceptionReason");
                line.Status = ReceiptLineStatus.EXCEPTION;
            }

            _db.QualityChecks.Add(check);
            await _db.SaveChangesAsync(ct);
            if (result == QualityCheckResult.EXCEPTION)
                await _attachments.ClaimAsync(photoIds, "EXCEPTION", check.Id, operatorId, ct);

            await AdvanceReceiptStatusAsync(line.ReceiptId, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await GetAsync(line.ReceiptId, ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PagedResult<QualityExceptionItem>> SearchQualityExceptionsAsync(QualityExceptionSearchRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = request.PageSize is null or <= 0 ? 20 : request.PageSize.Value;
        var query = _db.QualityChecks
            .Include(x => x.ReceiptLine).ThenInclude(x => x.Receipt).ThenInclude(x => x.Warehouse)
            .Include(x => x.ReceiptLine).ThenInclude(x => x.Material)
            .Include(x => x.ReceiptLine).ThenInclude(x => x.Batch)
            .AsNoTracking()
            .Where(x => x.Result == QualityCheckResult.EXCEPTION)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(x => x.ReceiptLine.Receipt.WarehouseId == request.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(request.ResolutionStatus))
        {
            var pending = request.ResolutionStatus.Equals("PENDING", StringComparison.OrdinalIgnoreCase);
            query = pending ? query.Where(x => x.ResolutionAction == null) : query.Where(x => x.ResolutionAction != null);
        }
        if (!string.IsNullOrWhiteSpace(request.ExceptionReason))
            query = query.Where(x => x.ExceptionReason == InboundOrderService.ParseEnum<QualityExceptionReason>(request.ExceptionReason, "exceptionReason"));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLower();
            query = query.Where(x => x.ReceiptLine.Receipt.ReceiptNo.ToLower().Contains(kw) ||
                                     x.ReceiptLine.Material.Code.ToLower().Contains(kw) ||
                                     x.ReceiptLine.Material.Name.ToLower().Contains(kw) ||
                                     x.ReceiptLine.Batch.BatchNo.ToLower().Contains(kw));
        }

        var total = await query.CountAsync(ct);
        var checks = await query.OrderByDescending(x => x.CheckedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<QualityExceptionItem>(checks.Select(MapException).ToList(), total, page, pageSize);
    }

    public async Task<ReceiptItem> ResolveQualityCheckAsync(Guid checkId, ResolveQualityCheckRequest request, Guid operatorId, string operatorName, CancellationToken ct = default)
    {
        await using var tx = await BusinessTransaction.BeginAsync(_db, IsolationLevel.ReadCommitted, ct);
        try
        {
            var action = InboundOrderService.ParseEnum<QualityResolutionAction>(request.Action, "action");
            if (action == QualityResolutionAction.REJECT && string.IsNullOrWhiteSpace(request.Note))
                throw new DomainException("VALIDATION_ERROR", "REJECT 备注必填", 400);

            var resolvedAt = DateTime.UtcNow;
            var updated = await _db.QualityChecks
                .Where(x => x.Id == checkId &&
                            x.Result == QualityCheckResult.EXCEPTION &&
                            x.ResolutionAction == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ResolutionAction, action)
                    .SetProperty(x => x.ResolutionNote, request.Note)
                    .SetProperty(x => x.ResolvedBy, operatorId)
                    .SetProperty(x => x.ResolvedByName, operatorName)
                    .SetProperty(x => x.ResolvedAt, resolvedAt), ct);
            if (updated == 0)
            {
                var exists = await _db.QualityChecks.AsNoTracking().AnyAsync(x => x.Id == checkId, ct);
                if (!exists)
                    throw new DomainException("QUALITY_CHECK_NOT_FOUND", "质检记录不存在", 404);
                throw new DomainException("QUALITY_CHECK_ALREADY_RESOLVED", "质检异常已处理", 409);
            }

            var check = await _db.QualityChecks
                .Include(x => x.ReceiptLine).ThenInclude(x => x.Receipt)
                .AsNoTracking()
                .FirstAsync(x => x.Id == checkId, ct);

            if (action == QualityResolutionAction.PASS)
            {
                var line = await LoadReceiptLineForWriteAsync(check.ReceiptLineId, ct)
                    ?? throw new DomainException("QC_STATUS_INVALID", "收货行不存在", 409);
                var lineUpdated = await _db.ReceiptLines
                    .Where(x => x.Id == line.Id && x.Status == ReceiptLineStatus.EXCEPTION)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, ReceiptLineStatus.CHECKED), ct);
                if (lineUpdated == 0)
                    throw new DomainException("QC_STATUS_INVALID", "当前行不可放行", 409);
                line.Status = ReceiptLineStatus.CHECKED;
                try
                {
                    await MoveInventoryAsync(
                        line.Receipt.WarehouseId,
                        line.MaterialId,
                        line.BatchId,
                        line.Receipt.StagingLocationId,
                        line.Receipt.StagingLocationId,
                        StockSubjectStatus.PENDING_INSPECTION,
                        StockSubjectStatus.AVAILABLE,
                        line.ActualQty,
                        TxnGroupType.QUALITY_CHECK,
                        LedgerReason.QUALITY_CHECK,
                        line.Receipt.SourceDocType.ToString(),
                        line.Receipt.ReceiptNo,
                        operatorId,
                        null,
                        tx.Transaction,
                        ct);
                }
                catch (DomainException ex) when (ex.Code is "INSUFFICIENT_STOCK" or "VERSION_CONFLICT")
                {
                    throw new DomainException("QC_STATUS_INVALID", "待检库存状态不允许放行", 409);
                }
            }

            await AdvanceReceiptStatusAsync(check.ReceiptLine.ReceiptId, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await GetAsync(check.ReceiptLine.ReceiptId, ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PagedResult<PutawayTodoItem>> SearchPutawayTodosAsync(PutawayTodoSearchRequest request, CancellationToken ct = default)
    {
        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = request.PageSize is null or <= 0 ? 20 : request.PageSize.Value;
        var query = _db.ReceiptLines
            .Include(x => x.Receipt).ThenInclude(x => x.Warehouse)
            .Include(x => x.Receipt).ThenInclude(x => x.StagingLocation)
            .Include(x => x.Material)
            .Include(x => x.Batch)
            .AsNoTracking()
            .Where(x => x.Status == ReceiptLineStatus.CHECKED && x.Receipt.Status == ReceiptStatus.PUTAWAY)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(x => x.Receipt.WarehouseId == request.WarehouseId.Value);
        if (request.MaterialId.HasValue)
            query = query.Where(x => x.MaterialId == request.MaterialId.Value);
        if (request.BatchId.HasValue)
            query = query.Where(x => x.BatchId == request.BatchId.Value);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLower();
            query = query.Where(x => x.Receipt.ReceiptNo.ToLower().Contains(kw) ||
                                     x.Material.Code.ToLower().Contains(kw) ||
                                     x.Material.Name.ToLower().Contains(kw) ||
                                     x.Batch.BatchNo.ToLower().Contains(kw));
        }

        var total = await query.CountAsync(ct);
        var lines = await query.OrderBy(x => x.ReceivedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = new List<PutawayTodoItem>();
        foreach (var line in lines)
            items.Add(await MapPutawayTodoAsync(line, ct));
        return new PagedResult<PutawayTodoItem>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<LocationRecommendationItem>> GetRecommendationsAsync(Guid receiptLineId, CancellationToken ct = default)
    {
        var line = await _db.ReceiptLines
            .Include(x => x.Receipt)
            .Include(x => x.Material)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == receiptLineId, ct)
            ?? throw new DomainException("RECEIPT_LINE_NOT_FOUND", "收货行不存在", 404);

        var sameMaterialLocations = await _db.PhysicalInventories.AsNoTracking()
            .Include(x => x.Subject)
            .Where(x => x.Quantity > 0 &&
                        x.Subject.WarehouseId == line.Receipt.WarehouseId &&
                        x.Subject.MaterialId == line.MaterialId &&
                        x.Subject.Status == StockSubjectStatus.AVAILABLE)
            .Select(x => x.LocationId)
            .Distinct()
            .ToListAsync(ct);

        var locations = await _db.Locations.AsNoTracking()
            .Where(x => x.WarehouseId == line.Receipt.WarehouseId && x.Status == MaterialStatus.ENABLED && x.Type == LocationType.DEFAULT)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

        return locations
            .OrderByDescending(x => sameMaterialLocations.Contains(x.Id))
            .ThenBy(x => x.Code)
            .Select((x, index) =>
            {
                var same = sameMaterialLocations.Contains(x.Id);
                return new LocationRecommendationItem(
                    x.Id,
                    x.Code,
                    same ? "SAME_MATERIAL" : "FALLBACK",
                    same ? "同物料集中" : "默认库位",
                    index == 0);
            })
            .ToList();
    }

    public async Task<ReceiptItem> CreatePutawayRecordAsync(CreatePutawayRecordRequest request, Guid operatorId, string operatorName, CancellationToken ct = default)
    {
        await using var tx = await BusinessTransaction.BeginAsync(_db, IsolationLevel.ReadCommitted, ct);
        try
        {
            await LockReceiptLineAsync(request.ReceiptLineId, ct);
            var line = await LoadReceiptLineForWriteAsync(request.ReceiptLineId, ct)
                ?? throw new DomainException("RECEIPT_LINE_NOT_FOUND", "收货行不存在", 404);
            if (line.Status != ReceiptLineStatus.CHECKED || line.Receipt.Status != ReceiptStatus.PUTAWAY)
                throw new DomainException("VERSION_CONFLICT", "该任务已被处理或状态已变化", 409);

            var toLocation = await _db.Locations.FirstOrDefaultAsync(x => x.Id == request.ToLocationId, ct)
                ?? throw new DomainException("PUTAWAY_LOCATION_INVALID", "目标库位不存在", 400);
            if (toLocation.WarehouseId != line.Receipt.WarehouseId ||
                toLocation.Status != MaterialStatus.ENABLED ||
                toLocation.Type != LocationType.DEFAULT ||
                !toLocation.Code.Equals(request.ScannedLocationCode, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("PUTAWAY_LOCATION_INVALID", "目标库位不合法", 400);

            if (await _db.PutawayRecords.AnyAsync(x => x.ReceiptLineId == line.Id, ct))
                throw new DomainException("VERSION_CONFLICT", "该行已上架", 409);

            await MoveInventoryAsync(
                line.Receipt.WarehouseId,
                line.MaterialId,
                line.BatchId,
                line.Receipt.StagingLocationId,
                toLocation.Id,
                StockSubjectStatus.AVAILABLE,
                StockSubjectStatus.AVAILABLE,
                line.ActualQty,
                TxnGroupType.PUTAWAY,
                LedgerReason.PUTAWAY,
                line.Receipt.SourceDocType.ToString(),
                line.Receipt.ReceiptNo,
                operatorId,
                request.ExpectedInventoryVersion,
                tx.Transaction,
                ct);

            var subject = await GetOrCreateSubjectAsync(line.Receipt.WarehouseId, line.MaterialId, line.BatchId, StockSubjectStatus.AVAILABLE, ct);
            _db.PutawayRecords.Add(new PutawayRecord
            {
                ReceiptLineId = line.Id,
                SubjectId = subject.Id,
                FromLocationId = line.Receipt.StagingLocationId,
                ToLocationId = toLocation.Id,
                Quantity = line.ActualQty,
                RecommendedLocationId = (await GetRecommendationsAsync(line.Id, ct)).FirstOrDefault(x => x.Recommended)?.LocationId,
                SourceInventoryVersion = request.ExpectedInventoryVersion,
                OperatorId = operatorId,
                OperatorName = operatorName,
                PutawayAt = DateTime.UtcNow
            });
            line.Status = ReceiptLineStatus.PUTAWAY_DONE;

            await AdvanceReceiptStatusAsync(line.ReceiptId, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await GetAsync(line.ReceiptId, ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private IQueryable<Receipt> ReceiptQuery() =>
        _db.Receipts
            .Include(x => x.Warehouse)
            .Include(x => x.StagingLocation)
            .Include(x => x.Lines).ThenInclude(x => x.Material)
            .Include(x => x.Lines).ThenInclude(x => x.Batch);

    private async Task<IReadOnlyList<ReceiptItem>> MapReceiptsAsync(IEnumerable<Receipt> receipts, CancellationToken ct)
    {
        var list = receipts.ToList();
        var ids = list.Select(x => x.Id).ToList();
        var photos = await _db.Attachments.AsNoTracking()
            .Where(x => x.BizType == "RECEIPT" && x.BizId != null && ids.Contains(x.BizId.Value))
            .GroupBy(x => x.BizId!.Value)
            .ToDictionaryAsync(x => x.Key, x => (IReadOnlyList<Guid>)x.Select(a => a.Id).ToList(), ct);

        return list.Select(receipt => new ReceiptItem(
            receipt.Id,
            receipt.ReceiptNo,
            receipt.WarehouseId,
            receipt.Warehouse.Code,
            receipt.InboundOrderId,
            receipt.SourceDocType.ToString(),
            receipt.SourceDocNo,
            receipt.SourceType?.ToString(),
            receipt.SourceCode,
            receipt.Status.ToString(),
            receipt.Lines.OrderBy(x => x.LineNo).Select(line => new ReceiptLineItem(
                line.Id,
                line.LineNo,
                line.OrderLineId,
                line.OrderLineNo,
                line.MaterialId,
                line.Material.Code,
                line.Material.Name,
                line.BatchId,
                line.Batch.BatchNo,
                line.ExpectedQty.HasValue ? InboundOrderService.FormatQty(line.ExpectedQty.Value) : null,
                InboundOrderService.FormatQty(line.ActualQty),
                line.QtyDiff.HasValue ? InboundOrderService.FormatQty(line.QtyDiff.Value) : null,
                line.Status.ToString(),
                line.SourceBatchNo,
                line.ProductionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                line.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).ToList(),
            receipt.StagingLocationId,
            receipt.StagingLocation.Code,
            photos.GetValueOrDefault(receipt.Id, Array.Empty<Guid>()),
            receipt.OperatorId,
            receipt.OperatorName,
            receipt.OccurredAt)).ToList();
    }

    private static QualityTodoItem MapQualityTodo(ReceiptLine line) =>
        new(
            line.Id,
            line.ReceiptId,
            line.Receipt.ReceiptNo,
            line.Receipt.WarehouseId,
            line.Receipt.Warehouse.Code,
            line.MaterialId,
            line.Material.Code,
            line.Material.Name,
            line.BatchId,
            line.Batch.BatchNo,
            InboundOrderService.FormatQty(line.ActualQty),
            line.ReceivedAt);

    private static QualityExceptionItem MapException(QualityCheck check) =>
        new(
            check.Id,
            check.ReceiptLineId,
            check.ReceiptLine.Receipt.ReceiptNo,
            check.ReceiptLine.Receipt.WarehouseId,
            check.ReceiptLine.Receipt.Warehouse.Code,
            check.ReceiptLine.Material.Code,
            check.ReceiptLine.Material.Name,
            check.ReceiptLine.Batch.BatchNo,
            InboundOrderService.FormatQty(check.CheckedQty),
            check.ExceptionReason?.ToString() ?? string.Empty,
            check.Note,
            JsonSerializer.Deserialize<List<Guid>>(check.PhotoIdsJson) ?? new List<Guid>(),
            check.OperatorId,
            check.OperatorName,
            check.CheckedAt,
            check.ResolutionAction?.ToString(),
            check.ResolutionNote,
            check.ResolvedBy,
            check.ResolvedByName,
            check.ResolvedAt);

    private async Task<PutawayTodoItem> MapPutawayTodoAsync(ReceiptLine line, CancellationToken ct)
    {
        var subject = await _db.StockSubjects.AsNoTracking()
            .FirstAsync(x => x.WarehouseId == line.Receipt.WarehouseId &&
                             x.MaterialId == line.MaterialId &&
                             x.BatchId == line.BatchId &&
                             x.Status == StockSubjectStatus.AVAILABLE, ct);
        var inventory = await _db.PhysicalInventories.AsNoTracking()
            .FirstAsync(x => x.LocationId == line.Receipt.StagingLocationId && x.SubjectId == subject.Id, ct);

        return new PutawayTodoItem(
            line.Id,
            line.Receipt.ReceiptNo,
            line.Receipt.WarehouseId,
            line.Receipt.Warehouse.Code,
            line.MaterialId,
            line.Material.Code,
            line.Material.Name,
            line.BatchId,
            line.Batch.BatchNo,
            InboundOrderService.FormatQty(line.ActualQty),
            line.Material.DefaultQtyPerLabel.HasValue ? InboundOrderService.FormatQty(line.Material.DefaultQtyPerLabel.Value) : null,
            line.Receipt.StagingLocationId,
            line.Receipt.StagingLocation.Code,
            inventory.Version);
    }

    private async Task<Warehouse> RequireWarehouseAsync(Guid warehouseId, CancellationToken ct)
    {
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId, ct)
            ?? throw new DomainException("WAREHOUSE_NOT_FOUND", "仓库不存在", 404);
        if (warehouse.Status != MaterialStatus.ENABLED)
            throw new DomainException("WAREHOUSE_NOT_FOUND", "仓库不可用", 404);
        return warehouse;
    }

    private async Task<Material> RequireMaterialAsync(Guid materialId, CancellationToken ct)
    {
        var material = await _db.Materials.FirstOrDefaultAsync(x => x.Id == materialId, ct)
            ?? throw new DomainException("MATERIAL_NOT_FOUND", "物料不存在", 404);
        if (material.Status != MaterialStatus.ENABLED)
            throw new DomainException("MATERIAL_NOT_FOUND", "物料不可用", 404);
        return material;
    }

    private async Task<Location> RequireStagingLocationAsync(Guid warehouseId, Guid locationId, CancellationToken ct)
    {
        var location = await _db.Locations.FirstOrDefaultAsync(x => x.Id == locationId, ct)
            ?? throw new DomainException("STAGING_LOCATION_INVALID", "暂存库位不存在", 400);
        if (location.WarehouseId != warehouseId || location.Status != MaterialStatus.ENABLED || location.Type != LocationType.STAGING)
            throw new DomainException("STAGING_LOCATION_INVALID", "暂存库位不合法", 400);
        return location;
    }

    private async Task<InboundOrder?> LoadOrderForWriteAsync(Guid id, CancellationToken ct) =>
        await _db.InboundOrders
            .Include(x => x.Lines).ThenInclude(x => x.Material)
            .Include(x => x.Lines).ThenInclude(x => x.UniqueCodes)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    private async Task<ReceiptLine?> LoadReceiptLineForWriteAsync(Guid id, CancellationToken ct) =>
        await _db.ReceiptLines
            .Include(x => x.Receipt).ThenInclude(x => x.StagingLocation)
            .Include(x => x.Material)
            .Include(x => x.Batch)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    private async Task LockOrderAsync(Guid orderId, CancellationToken ct) =>
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"InboundOrders\" WHERE \"Id\" = {orderId} FOR UPDATE", ct);

    private async Task LockReceiptLineAsync(Guid lineId, CancellationToken ct) =>
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"ReceiptLines\" WHERE \"Id\" = {lineId} FOR UPDATE", ct);

    private async Task<(SourceType? SourceType, string? SourceCode)> ValidateReceiptHeaderSourceAsync(InboundOrderType sourceDocType, string? sourceTypeValue, string? sourceCode, CancellationToken ct)
    {
        if (sourceDocType == InboundOrderType.PR)
            return (SourceType.WORKSHOP, await RequireSourceAsync(SourceType.WORKSHOP, sourceTypeValue, sourceCode, ct));

        if (string.IsNullOrWhiteSpace(sourceTypeValue) && string.IsNullOrWhiteSpace(sourceCode))
            return (null, null);
        if (string.IsNullOrWhiteSpace(sourceTypeValue) || string.IsNullOrWhiteSpace(sourceCode))
            throw new DomainException("VALIDATION_ERROR", "sourceType/sourceCode 必须同时为空或同时有值", 400);
        var parsed = InboundOrderService.ParseEnum<SourceType>(sourceTypeValue, "sourceType");
        return (parsed, await RequireSourceAsync(parsed, parsed.ToString(), sourceCode, ct));
    }

    private async Task<string> RequireSourceAsync(SourceType expected, string? sourceTypeValue, string? sourceCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new DomainException("VALIDATION_ERROR", "sourceCode 必填", 400);
        if (string.IsNullOrWhiteSpace(sourceTypeValue) || !Enum.TryParse<SourceType>(sourceTypeValue, true, out var parsed) || parsed != expected)
            throw new DomainException("VALIDATION_ERROR", $"sourceType 必须为 {expected}", 400);
        var source = await _db.Sources.FirstOrDefaultAsync(x => x.Type == expected && x.Code == sourceCode, ct)
            ?? throw new DomainException("SOURCE_NOT_FOUND", "来源不存在", 404);
        if (source.Status != MaterialStatus.ENABLED)
            throw new DomainException("SOURCE_NOT_FOUND", "来源不可用", 404);
        return source.Code;
    }

    private static InboundOrderLine? ResolveOrderLine(InboundOrder? order, SubmitReceiptLineRequest request, Guid materialId)
    {
        if (order == null)
            return null;
        if (request.OrderLineId.HasValue)
        {
            var byId = order.Lines.SingleOrDefault(x => x.Id == request.OrderLineId.Value);
            if (byId == null || byId.MaterialId != materialId)
                throw new DomainException("ORDER_LINE_MISMATCH", "收货行与入库单不匹配", 400);
            return byId;
        }

        var matches = order.Lines.Where(x => x.MaterialId == materialId).ToList();
        if (matches.Count != 1)
            throw new DomainException("ORDER_LINE_MISMATCH", "同物料多行必须指定 orderLineId", 400);
        return matches[0];
    }

    private async Task ValidateOrderLineQuantityAsync(
        InboundOrder? order,
        InboundOrderLine? orderLine,
        decimal quantity,
        CancellationToken ct)
    {
        if (order == null || orderLine == null)
            return;

        var alreadyReceived = await _db.ReceiptLines.AsNoTracking()
            .Where(x => x.OrderLineId == orderLine.Id)
            .SumAsync(x => x.ActualQty, ct);
        if (order.Type == InboundOrderType.PO && (quantity != orderLine.ExpectedQty || alreadyReceived > 0))
            throw new DomainException("QTY_MISMATCH_STRICT", "PO 行必须一次收齐且数量等于应到", 400);
    }

    private async Task ValidateUniqueCodesAsync(InboundOrder? order, InboundOrderLine? orderLine, Material material, IReadOnlyList<string>? uniqueCodes, decimal quantity, CancellationToken ct)
    {
        var suppliedCodes = uniqueCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        var codes = suppliedCodes.Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count != suppliedCodes.Count)
            throw new DomainException("UNIQUE_CODE_ALREADY_RECEIVED", "同一请求不得重复提交唯一码", 409);
        if (material.LabelType != LabelType.UNIQUE)
        {
            if (codes.Count > 0)
                throw new DomainException("VALIDATION_ERROR", "非唯一码物料不得传 uniqueCodes", 400);
            return;
        }
        if (order == null || orderLine == null)
            throw new DomainException("UNIQUE_CODE_NOT_IN_ORDER", "唯一码物料必须来自预建单登记清单", 400);
        if (codes.Count == 0)
            throw new DomainException("VALIDATION_ERROR", "唯一码物料必须传 uniqueCodes", 400);

        var registered = await _db.UniqueCodes.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
        if (registered.Count != codes.Count || registered.Any(x => x.OrderLineId != orderLine.Id))
            throw new DomainException("UNIQUE_CODE_NOT_IN_ORDER", "唯一码不在当前入库单行", 400);
        if (registered.Any(x => x.Status == UniqueCodeStatus.RECEIVED))
            throw new DomainException("UNIQUE_CODE_ALREADY_RECEIVED", "唯一码已收货", 409);
        if (registered.Sum(x => x.Quantity) != quantity)
            throw new DomainException("UNIQUE_CODE_QTY_MISMATCH", "唯一码数量合计与实收数量不一致", 400);
    }

    private async Task MarkUniqueCodesReceivedAsync(IEnumerable<SubmitReceiptLineRequest> lines, DateTime receivedAt, CancellationToken ct)
    {
        var codes = lines.SelectMany(x => x.UniqueCodes ?? Array.Empty<string>()).Distinct().ToList();
        if (codes.Count == 0)
            return;
        var registered = await _db.UniqueCodes.Where(x => codes.Contains(x.Code)).ToListAsync(ct);
        foreach (var code in registered)
        {
            code.Status = UniqueCodeStatus.RECEIVED;
            code.ReceivedAt = receivedAt;
        }
    }

    private async Task<Batch> ResolveBatchAsync(
        InboundOrderType sourceDocType,
        Material material,
        Guid? batchId,
        BatchPropsRequest? props,
        SourceType? headerSourceType,
        string? headerSourceCode,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        CancellationToken ct)
    {
        if (!material.BatchControlled)
        {
            if (batchId.HasValue || props != null)
                throw new DomainException("VALIDATION_ERROR", "非批控物料不得传 batchId/batchProps", 400);
            return await GetOrCreateDefaultBatchAsync(material, ct);
        }

        if (batchId.HasValue == (props != null))
            throw new DomainException("BATCH_REQUIRED", "批控物料必须且只能提供 batchId 或 batchProps", 400);

        if (batchId.HasValue)
        {
            if (sourceDocType != InboundOrderType.PR)
                throw new DomainException("VALIDATION_ERROR", "batchId 仅用于 PR 生产退料", 400);
            var existing = await _db.Batches.FirstOrDefaultAsync(x => x.Id == batchId.Value && x.MaterialId == material.Id, ct)
                ?? throw new DomainException("BATCH_NOT_FOUND", "批次不存在", 404);
            return existing;
        }

        var (sourceType, sourceCode) = await ResolveBatchSourceAsync(props!, headerSourceType, headerSourceCode, ct);
        var batch = new Batch
        {
            MaterialId = material.Id,
            MaterialCode = material.Code,
            BatchNo = await _numbering.NextAsyncCore("BATCH", material.Code, tx),
            SourceBatchNo = props!.SourceBatchNo,
            ProductionDate = ParseDate(props.ProductionDate, "productionDate"),
            ExpiryDate = ParseDate(props.ExpiryDate, "expiryDate"),
            SourceType = sourceType?.ToString(),
            SourceCode = sourceCode,
            Status = BatchStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Batches.Add(batch);
        await _db.SaveChangesAsync(ct);
        return batch;
    }

    private async Task<Batch> GetOrCreateDefaultBatchAsync(Material material, CancellationToken ct)
    {
        var batch = await _db.Batches.FirstOrDefaultAsync(x => x.MaterialId == material.Id && x.BatchNo == "DEFAULT", ct);
        if (batch != null)
            return batch;
        batch = new Batch
        {
            MaterialId = material.Id,
            MaterialCode = material.Code,
            BatchNo = "DEFAULT",
            Status = BatchStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Batches.Add(batch);
        await _db.SaveChangesAsync(ct);
        return batch;
    }

    private async Task<(SourceType? SourceType, string? SourceCode)> ResolveBatchSourceAsync(BatchPropsRequest props, SourceType? headerSourceType, string? headerSourceCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(props.SourceType) && string.IsNullOrWhiteSpace(props.SourceCode))
            return (headerSourceType, headerSourceCode);
        if (string.IsNullOrWhiteSpace(props.SourceType) || string.IsNullOrWhiteSpace(props.SourceCode))
            throw new DomainException("VALIDATION_ERROR", "batchProps.sourceType/sourceCode 必须成对", 400);

        var parsed = InboundOrderService.ParseEnum<SourceType>(props.SourceType, "sourceType");
        var code = await RequireSourceAsync(parsed, parsed.ToString(), props.SourceCode, ct);
        if (headerSourceType.HasValue && (headerSourceType.Value != parsed || !string.Equals(headerSourceCode, code, StringComparison.Ordinal)))
            throw new DomainException("SOURCE_MISMATCH", "批次来源与单据来源不一致", 400);
        return (parsed, code);
    }

    private static DateOnly? ParseDate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new DomainException("VALIDATION_ERROR", $"{fieldName} 日期格式应为 yyyy-MM-dd", 400);
        return date;
    }

    private async Task<StockSubject> GetOrCreateSubjectAsync(Guid warehouseId, Guid materialId, Guid batchId, StockSubjectStatus status, CancellationToken ct)
    {
        var subject = await _db.StockSubjects.AsNoTracking().FirstOrDefaultAsync(
            x => x.WarehouseId == warehouseId && x.MaterialId == materialId && x.BatchId == batchId && x.Status == status,
            ct);
        if (subject != null)
            return subject;

        var material = await _db.Materials.AsNoTracking().FirstAsync(x => x.Id == materialId, ct);
        var id = Guid.CreateVersion7();
        var transaction = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("库存主体创建必须在事务内执行");
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            INSERT INTO "StockSubjects" ("Id", "WarehouseId", "MaterialId", "BatchId", "Status", "Uom")
            VALUES (@id, @warehouseId, @materialId, @batchId, @status, @uom)
            ON CONFLICT ("WarehouseId", "MaterialId", "BatchId", "Status") DO NOTHING
            RETURNING "Id"
            """;
        command.Parameters.Add(new NpgsqlParameter("id", id));
        command.Parameters.Add(new NpgsqlParameter("warehouseId", warehouseId));
        command.Parameters.Add(new NpgsqlParameter("materialId", materialId));
        command.Parameters.Add(new NpgsqlParameter("batchId", batchId));
        command.Parameters.Add(new NpgsqlParameter("status", status.ToString()));
        command.Parameters.Add(new NpgsqlParameter("uom", material.DefaultUom));
        var inserted = await command.ExecuteScalarAsync(ct);
        var subjectId = inserted is Guid insertedId
            ? insertedId
            : await _db.StockSubjects.AsNoTracking()
                .Where(x => x.WarehouseId == warehouseId &&
                            x.MaterialId == materialId &&
                            x.BatchId == batchId &&
                            x.Status == status)
                .Select(x => x.Id)
                .SingleAsync(ct);
        return new StockSubject
        {
            Id = subjectId,
            WarehouseId = warehouseId,
            MaterialId = materialId,
            BatchId = batchId,
            Status = status,
            Uom = material.DefaultUom
        };
    }

    private async Task AddInventoryAsync(
        Guid warehouseId,
        Guid materialId,
        Guid batchId,
        StockSubjectStatus status,
        Guid locationId,
        decimal quantity,
        TxnGroupType groupType,
        LedgerReason reason,
        string sourceDocType,
        string sourceDocNo,
        Guid operatorId,
        IDbContextTransaction tx,
        CancellationToken ct)
    {
        var subject = await GetOrCreateSubjectAsync(warehouseId, materialId, batchId, status, ct);
        var inventory = await IncrementInventoryAsync(locationId, subject.Id, quantity, ct);
        var group = await CreateTxnGroupAsync(groupType, tx, ct);
        _db.StockLedgers.Add(new StockLedger
        {
            TxnGroupId = group.Id,
            Seq = 1,
            SubjectId = subject.Id,
            LocationId = locationId,
            Quantity = quantity,
            BalanceBefore = inventory.BalanceBefore,
            BalanceAfter = inventory.BalanceAfter,
            Reason = reason,
            SourceDocType = sourceDocType,
            SourceDocNo = sourceDocNo,
            OperatorId = operatorId,
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task MoveInventoryAsync(
        Guid warehouseId,
        Guid materialId,
        Guid batchId,
        Guid fromLocationId,
        Guid toLocationId,
        StockSubjectStatus fromStatus,
        StockSubjectStatus toStatus,
        decimal quantity,
        TxnGroupType groupType,
        LedgerReason reason,
        string sourceDocType,
        string sourceDocNo,
        Guid operatorId,
        int? expectedFromVersion,
        IDbContextTransaction tx,
        CancellationToken ct)
    {
        var fromSubject = await GetOrCreateSubjectAsync(warehouseId, materialId, batchId, fromStatus, ct);
        var fromInventory = await DecrementInventoryAsync(
            fromLocationId,
            fromSubject.Id,
            quantity,
            expectedFromVersion,
            ct);
        var toSubject = await GetOrCreateSubjectAsync(warehouseId, materialId, batchId, toStatus, ct);
        var toInventory = await IncrementInventoryAsync(toLocationId, toSubject.Id, quantity, ct);
        var group = await CreateTxnGroupAsync(groupType, tx, ct);

        _db.StockLedgers.Add(new StockLedger
        {
            TxnGroupId = group.Id,
            Seq = 1,
            SubjectId = fromSubject.Id,
            LocationId = fromLocationId,
            Quantity = -quantity,
            BalanceBefore = fromInventory.BalanceBefore,
            BalanceAfter = fromInventory.BalanceAfter,
            Reason = reason,
            SourceDocType = sourceDocType,
            SourceDocNo = sourceDocNo,
            OperatorId = operatorId,
            OccurredAt = DateTime.UtcNow
        });

        _db.StockLedgers.Add(new StockLedger
        {
            TxnGroupId = group.Id,
            Seq = 2,
            SubjectId = toSubject.Id,
            LocationId = toLocationId,
            Quantity = quantity,
            BalanceBefore = toInventory.BalanceBefore,
            BalanceAfter = toInventory.BalanceAfter,
            Reason = reason,
            SourceDocType = sourceDocType,
            SourceDocNo = sourceDocNo,
            OperatorId = operatorId,
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<InventoryMutation> IncrementInventoryAsync(
        Guid locationId,
        Guid subjectId,
        decimal quantity,
        CancellationToken ct)
    {
        var transaction = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("库存更新必须在事务内执行");
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            INSERT INTO "PhysicalInventories" ("Id", "LocationId", "SubjectId", "Quantity", "Version")
            VALUES (@id, @locationId, @subjectId, @quantity, 1)
            ON CONFLICT ("LocationId", "SubjectId") DO UPDATE
            SET "Quantity" = "PhysicalInventories"."Quantity" + EXCLUDED."Quantity",
                "Version" = "PhysicalInventories"."Version" + 1
            RETURNING "Quantity", "Version"
            """;
        command.Parameters.Add(new NpgsqlParameter("id", Guid.CreateVersion7()));
        command.Parameters.Add(new NpgsqlParameter("locationId", locationId));
        command.Parameters.Add(new NpgsqlParameter("subjectId", subjectId));
        command.Parameters.Add(new NpgsqlParameter("quantity", quantity));
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var balanceAfter = reader.GetDecimal(0);
        var versionAfter = reader.GetInt32(1);
        return new InventoryMutation(balanceAfter - quantity, balanceAfter, versionAfter);
    }

    private async Task<InventoryMutation> DecrementInventoryAsync(
        Guid locationId,
        Guid subjectId,
        decimal quantity,
        int? expectedVersion,
        CancellationToken ct)
    {
        var transaction = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("库存更新必须在事务内执行");
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            UPDATE "PhysicalInventories"
            SET "Quantity" = "Quantity" - @quantity,
                "Version" = "Version" + 1
            WHERE "LocationId" = @locationId
              AND "SubjectId" = @subjectId
              AND "Quantity" >= @quantity
            """ + (expectedVersion.HasValue ? " AND \"Version\" = @expectedVersion" : string.Empty) +
            " RETURNING \"Quantity\", \"Version\"";
        command.Parameters.Add(new NpgsqlParameter("locationId", locationId));
        command.Parameters.Add(new NpgsqlParameter("subjectId", subjectId));
        command.Parameters.Add(new NpgsqlParameter("quantity", quantity));
        if (expectedVersion.HasValue)
            command.Parameters.Add(new NpgsqlParameter("expectedVersion", expectedVersion.Value));

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var balanceAfter = reader.GetDecimal(0);
            var versionAfter = reader.GetInt32(1);
            return new InventoryMutation(balanceAfter + quantity, balanceAfter, versionAfter);
        }
        await reader.DisposeAsync();

        var current = await _db.PhysicalInventories.AsNoTracking()
            .Where(x => x.LocationId == locationId && x.SubjectId == subjectId)
            .Select(x => new { x.Quantity, x.Version })
            .SingleOrDefaultAsync(ct);
        if (expectedVersion.HasValue && current != null && current.Version != expectedVersion.Value)
            throw new DomainException("VERSION_CONFLICT", "库存版本已变化", 409);
        throw new DomainException("INSUFFICIENT_STOCK", "库存不足", 409);
    }

    private async Task<TxnGroup> CreateTxnGroupAsync(TxnGroupType type, IDbContextTransaction tx, CancellationToken ct)
    {
        var group = new TxnGroup
        {
            GroupNo = await _numbering.NextAsyncCore("TXN_GROUP", "GLOBAL", tx),
            Type = type,
            CreatedAt = DateTime.UtcNow
        };
        _db.TxnGroups.Add(group);
        await _db.SaveChangesAsync(ct);
        return group;
    }

    private sealed record InventoryMutation(decimal BalanceBefore, decimal BalanceAfter, int VersionAfter);

    private async Task AdvanceInboundOrderStatusAsync(InboundOrder order, CancellationToken ct)
    {
        var lineIds = order.Lines.Select(x => x.Id).ToList();
        var received = await _db.ReceiptLines
            .Where(x => x.OrderLineId != null && lineIds.Contains(x.OrderLineId.Value))
            .GroupBy(x => x.OrderLineId!.Value)
            .Select(x => new { OrderLineId = x.Key, Qty = x.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => x.OrderLineId, x => x.Qty, ct);

        order.Status = order.Lines.All(x => received.GetValueOrDefault(x.Id) >= x.ExpectedQty)
            ? InboundOrderStatus.RECEIVED
            : InboundOrderStatus.RECEIVING;
        order.UpdatedAt = DateTime.UtcNow;
    }

    private async Task AdvanceReceiptStatusAsync(Guid receiptId, CancellationToken ct)
    {
        var receipt = await _db.Receipts.Include(x => x.Lines).FirstAsync(x => x.Id == receiptId, ct);
        if (receipt.Lines.All(x => x.Status == ReceiptLineStatus.PUTAWAY_DONE) &&
            !receipt.Lines.Any(x => x.Status == ReceiptLineStatus.EXCEPTION))
        {
            receipt.Status = ReceiptStatus.DONE;
            return;
        }
        if (receipt.Lines.All(x => x.Status is ReceiptLineStatus.CHECKED or ReceiptLineStatus.PUTAWAY_DONE) &&
            !receipt.Lines.Any(x => x.Status == ReceiptLineStatus.EXCEPTION))
        {
            receipt.Status = ReceiptStatus.PUTAWAY;
            return;
        }
        receipt.Status = ReceiptStatus.CHECKING;
    }
}
