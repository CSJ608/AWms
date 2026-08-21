using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Domain.Dtos.Attachments;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Inbound;
using AWms.Domain.Dtos.Print;
using AWms.Domain.Dtos.Receipts;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AWms.IntegrationTests;

public class InboundReceiptChainApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public InboundReceiptChainApiTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task 黄金路径_PO收货质检上架_库存与流水同事务闭合()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var seed = await SeedAsync(batchControlled: true, labelType: LabelType.SKU);
        var order = await CreatePoOrderAsync(seed, "200.0000", "gold-order");
        var orderLine = order.Lines.Single();

        var receipt = await SubmitReceiptAsync(
            seed,
            order.Id,
            orderLine.Id,
            "200.0000",
            new BatchPropsRequest("PRD-260820-01", "2026-08-20", null, null, null),
            "gold-receipt");
        var receiptLine = receipt.Lines.Single();

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
            Assert.Equal(1, await db.Batches.CountAsync());
            Assert.Equal(1, await db.StockLedgers.CountAsync());
            var pending = await InventoryQtyAsync(db, seed.StagingLocationId, StockSubjectStatus.PENDING_INSPECTION);
            Assert.Equal(200m, pending);
        }

        var qc = await SendJsonAsync<ReceiptItem>(
            HttpMethod.Post,
            $"/api/receipt-lines/{receiptLine.Id}/quality-check",
            new QualityCheckRequest("PASS", "200.0000", null, null, null),
            "gold-qc");
        Assert.Equal("PUTAWAY", qc.Status);

        var todoResp = await _client.PostAsJsonAsync("/api/putaway-todos/search", new PutawayTodoSearchRequest(seed.WarehouseId, null, null, null, 1, 20));
        todoResp.EnsureSuccessStatusCode();
        var todos = (await todoResp.Content.ReadFromJsonAsync<ApiResponse<PagedResult<PutawayTodoItem>>>(JsonOpts))!.Data!;
        var todo = Assert.Single(todos.Items);

        var done = await SendJsonAsync<ReceiptItem>(
            HttpMethod.Post,
            "/api/putaway-records",
            new CreatePutawayRecordRequest(todo.ReceiptLineId, seed.DefaultLocationId, seed.DefaultLocationCode, todo.InventoryVersion),
            "gold-putaway");
        Assert.Equal("DONE", done.Status);

        using var finalScope = _fixture.Factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(0m, await InventoryQtyAsync(finalDb, seed.StagingLocationId, StockSubjectStatus.PENDING_INSPECTION));
        Assert.Equal(0m, await InventoryQtyAsync(finalDb, seed.StagingLocationId, StockSubjectStatus.AVAILABLE));
        Assert.Equal(200m, await InventoryQtyAsync(finalDb, seed.DefaultLocationId, StockSubjectStatus.AVAILABLE));
        Assert.Equal(5, await finalDb.StockLedgers.CountAsync());
        Assert.Equal(3, await finalDb.TxnGroups.CountAsync());
        Assert.Equal("RECEIVED", (await finalDb.InboundOrders.SingleAsync()).Status.ToString());
    }

    [Fact]
    public async Task PO数量不一致_失败且无批次库存流水部分写入()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var seed = await SeedAsync(batchControlled: true, labelType: LabelType.SKU);
        var order = await CreatePoOrderAsync(seed, "10.0000", "fail-order");

        var response = await SendJsonRawAsync(
            HttpMethod.Post,
            "/api/receipts",
            new SubmitReceiptRequest(
                seed.WarehouseId,
                seed.StagingLocationId,
                order.Id,
                null,
                null,
                null,
                null,
                new[]
                {
                    new SubmitReceiptLineRequest(order.Lines.Single().Id, seed.MaterialId, null, new BatchPropsRequest("BAD", null, null, null, null), "5.0000", null)
                },
                null),
            "fail-receipt");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("QTY_MISMATCH_STRICT", envelope!.Code);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(0, await db.Receipts.CountAsync());
        Assert.Equal(0, await db.Batches.CountAsync());
        Assert.Equal(0, await db.PhysicalInventories.CountAsync());
        Assert.Equal(0, await db.StockLedgers.CountAsync());
    }

    [Fact]
    public async Task 不同IdempotencyKey并发提交同一PO行_最多一个成功()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var seed = await SeedAsync(batchControlled: true, labelType: LabelType.SKU);
        var order = await CreatePoOrderAsync(seed, "10.0000", "concurrent-order");
        var line = order.Lines.Single();

        var body = new SubmitReceiptRequest(
            seed.WarehouseId,
            seed.StagingLocationId,
            order.Id,
            null,
            null,
            null,
            null,
            new[] { new SubmitReceiptLineRequest(line.Id, seed.MaterialId, null, new BatchPropsRequest("CON", null, null, null, null), "10.0000", null) },
            null);

        var responses = await Task.WhenAll(
            SendJsonRawAsync(HttpMethod.Post, "/api/receipts", body, "concurrent-r-1"),
            SendJsonRawAsync(HttpMethod.Post, "/api/receipts", body, "concurrent-r-2"));

        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.BadRequest || x.StatusCode == HttpStatusCode.Conflict);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(1, await db.Receipts.CountAsync());
        Assert.Equal(1, await db.ReceiptLines.CountAsync());
        Assert.Equal(1, await db.StockLedgers.CountAsync());
    }

    [Fact]
    public async Task 附件并发认领_一个成功一个失败且失败无部分写入()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var seed = await SeedAsync(batchControlled: true, labelType: LabelType.SKU, sourceType: SourceType.WORKSHOP, sourceCode: "WS-01");
        var attachment = await UploadPhotoAsync();
        var body = new SubmitReceiptRequest(
            seed.WarehouseId,
            seed.StagingLocationId,
            null,
            "PR",
            null,
            "WORKSHOP",
            "WS-01",
            new[] { new SubmitReceiptLineRequest(null, seed.MaterialId, null, new BatchPropsRequest("PR-BATCH", null, null, null, null), "3.0000", null) },
            new[] { attachment.Id });

        var responses = await Task.WhenAll(
            SendJsonRawAsync(HttpMethod.Post, "/api/receipts", body, "photo-claim-1"),
            SendJsonRawAsync(HttpMethod.Post, "/api/receipts", body, "photo-claim-2"));

        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Contains(responses, x => x.StatusCode == HttpStatusCode.Conflict);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(1, await db.Receipts.CountAsync());
        Assert.Equal(1, await db.StockLedgers.CountAsync());
        var linked = await db.Attachments.SingleAsync();
        Assert.Equal("RECEIPT", linked.BizType);
        Assert.NotNull(linked.BizId);
    }

    [Fact]
    public async Task 上架版本冲突_不创建上架记录且库存不变()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var seed = await SeedAsync(batchControlled: true, labelType: LabelType.SKU);
        var order = await CreatePoOrderAsync(seed, "8.0000", "version-order");
        var receipt = await SubmitReceiptAsync(seed, order.Id, order.Lines.Single().Id, "8.0000", new BatchPropsRequest("VC", null, null, null, null), "version-receipt");
        var receiptLine = receipt.Lines.Single();
        await SendJsonAsync<ReceiptItem>(
            HttpMethod.Post,
            $"/api/receipt-lines/{receiptLine.Id}/quality-check",
            new QualityCheckRequest("PASS", "8.0000", null, null, null),
            "version-qc");

        var bad = await SendJsonRawAsync(
            HttpMethod.Post,
            "/api/putaway-records",
            new CreatePutawayRecordRequest(receiptLine.Id, seed.DefaultLocationId, seed.DefaultLocationCode, -1),
            "version-putaway");

        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
        var envelope = await bad.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("VERSION_CONFLICT", envelope!.Code);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(0, await db.PutawayRecords.CountAsync());
        Assert.Equal(8m, await InventoryQtyAsync(db, seed.StagingLocationId, StockSubjectStatus.AVAILABLE));
        Assert.Equal(0m, await InventoryQtyAsync(db, seed.DefaultLocationId, StockSubjectStatus.AVAILABLE));
    }

    [Fact]
    public async Task 唯一码打印_登记数量与PrintJob同事务()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var seed = await SeedAsync(batchControlled: true, labelType: LabelType.UNIQUE);
        var order = await CreatePoOrderAsync(seed, "10.0000", "unique-order");

        var job = await SendJsonAsync<PrintJobDto>(
            HttpMethod.Post,
            "/api/print/unique-labels",
            new UniqueLabelsPrintRequest(order.Lines.Single().Id, 2, "5.0000"),
            "unique-print");

        Assert.Equal("READY", job.Status);
        Assert.Equal(2, job.Items.Count);
        Assert.NotNull(job.FileUrl);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(2, await db.UniqueCodes.CountAsync());
        Assert.Equal(10m, await db.UniqueCodes.SumAsync(x => x.Quantity));
        Assert.Equal(1, await db.PrintJobs.CountAsync());
        Assert.Equal(2, await db.PrintJobItems.CountAsync());
    }

    private async Task LoginAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiTestFixture.AdminUsername,
            password = ApiTestFixture.AdminPassword
        });
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope!.Data!.Token);
    }

    private async Task<SeedData> SeedAsync(
        bool batchControlled,
        LabelType labelType,
        SourceType sourceType = SourceType.SUPPLIER,
        string sourceCode = "SUP-001")
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        var suffix = Guid.CreateVersion7().ToString("N")[..8];
        var warehouse = new Warehouse { Code = $"WH-{suffix}", Name = "一号仓", Status = MaterialStatus.ENABLED };
        var material = new Material
        {
            Code = $"MAT-{suffix}",
            Name = "测试物料",
            BatchControlled = batchControlled,
            LabelType = labelType,
            DefaultUom = "CT",
            DefaultQtyPerLabel = 5,
            Status = MaterialStatus.ENABLED
        };
        var source = new Source
        {
            Type = sourceType,
            Code = sourceCode,
            Name = "测试来源",
            Status = MaterialStatus.ENABLED
        };
        db.Warehouses.Add(warehouse);
        db.Materials.Add(material);
        db.Sources.Add(source);
        await db.SaveChangesAsync();

        var staging = new Location { WarehouseId = warehouse.Id, Code = $"STG-{suffix}", Type = LocationType.STAGING, Status = MaterialStatus.ENABLED };
        var dft = new Location { WarehouseId = warehouse.Id, Code = $"DFT-{suffix}", Type = LocationType.DEFAULT, Status = MaterialStatus.ENABLED };
        db.Locations.AddRange(staging, dft);
        await db.SaveChangesAsync();
        return new SeedData(warehouse.Id, staging.Id, dft.Id, dft.Code, material.Id, sourceCode);
    }

    private async Task<InboundOrderItem> CreatePoOrderAsync(SeedData seed, string expectedQty, string idemKey) =>
        await SendJsonAsync<InboundOrderItem>(
            HttpMethod.Post,
            "/api/inbound-orders",
            new CreateInboundOrderRequest(
                "PO",
                seed.WarehouseId,
                "SUPPLIER",
                seed.SourceCode,
                new[] { new CreateInboundOrderLineRequest(seed.MaterialId, expectedQty) }),
            idemKey);

    private async Task<ReceiptItem> SubmitReceiptAsync(
        SeedData seed,
        Guid orderId,
        Guid orderLineId,
        string qty,
        BatchPropsRequest batchProps,
        string idemKey) =>
        await SendJsonAsync<ReceiptItem>(
            HttpMethod.Post,
            "/api/receipts",
            new SubmitReceiptRequest(
                seed.WarehouseId,
                seed.StagingLocationId,
                orderId,
                null,
                null,
                null,
                null,
                new[] { new SubmitReceiptLineRequest(orderLineId, seed.MaterialId, null, batchProps, qty, null) },
                null),
            idemKey);

    private async Task<AttachmentItem> UploadPhotoAsync()
    {
        using var form = new MultipartFormDataContent();
        using var image = new Image<Rgba32>(2, 2, new Rgba32(30, 120, 210));
        using var pngStream = new MemoryStream();
        image.Save(pngStream, new PngEncoder());
        var png = pngStream.ToArray();
        var content = new ByteArrayContent(png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "photo.png");
        form.Add(new StringContent("RECEIPT"), "bizType");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/attachments") { Content = form };
        request.Headers.Add("Idempotency-Key", $"attachment-{Guid.CreateVersion7():N}");
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AttachmentItem>>(JsonOpts))!.Data!;
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string path, object body, string idempotencyKey)
    {
        var response = await SendJsonRawAsync(method, path, body, idempotencyKey);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOpts))!.Data!;
    }

    private async Task<HttpResponseMessage> SendJsonRawAsync(HttpMethod method, string path, object body, string idempotencyKey)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(request);
    }

    private static async Task<decimal> InventoryQtyAsync(AWmsDbContext db, Guid locationId, StockSubjectStatus status)
    {
        var rows = await db.PhysicalInventories
            .Include(x => x.Subject)
            .Where(x => x.LocationId == locationId && x.Subject.Status == status)
            .ToListAsync();
        return rows.Sum(x => x.Quantity);
    }

    private record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);

    private record SeedData(Guid WarehouseId, Guid StagingLocationId, Guid DefaultLocationId, string DefaultLocationCode, Guid MaterialId, string SourceCode);
}
