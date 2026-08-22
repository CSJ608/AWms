using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AWms.Domain.Dtos.Attachments;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Inbound;
using AWms.Domain.Dtos.Print;
using AWms.Domain.Dtos.Receipts;
using AWms.Domain.Dtos.Scan;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;

namespace AWms.IntegrationTests;

public class InboundReceiptChainRemediationApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly byte[] TinyPng = CreateTinyPng();

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public InboundReceiptChainRemediationApiTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task 同一请求重复PO行_整体失败且无订单收货库存流水部分写入()
    {
        await ResetAndLoginAsync();
        var seed = await SeedAsync(true, LabelType.SKU);
        var order = await CreatePoOrderAsync(seed, "10.0000", Key());
        var line = order.Lines.Single();
        var body = new SubmitReceiptRequest(
            seed.WarehouseId,
            seed.StagingLocationId,
            order.Id,
            null,
            null,
            null,
            null,
            new[]
            {
                new SubmitReceiptLineRequest(line.Id, seed.MaterialId, null, new BatchPropsRequest("LOT-1", null, null, null, null), "10.0000", null),
                new SubmitReceiptLineRequest(line.Id, seed.MaterialId, null, new BatchPropsRequest("LOT-2", null, null, null, null), "10.0000", null)
            },
            null);

        var response = await SendJsonRawAsync(HttpMethod.Post, "/api/receipts", body, Key());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ORDER_LINE_MISMATCH", (await ErrorAsync(response)).Code);
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(InboundOrderStatus.CONFIRMED, (await db.InboundOrders.SingleAsync()).Status);
        Assert.Empty(await db.Receipts.ToListAsync());
        Assert.Empty(await db.Batches.ToListAsync());
        Assert.Empty(await db.PhysicalInventories.ToListAsync());
        Assert.Empty(await db.StockLedgers.ToListAsync());
    }

    [Fact]
    public async Task 共享PhysicalInventory并发质检_库存版本与流水余额链守恒()
    {
        await ResetAndLoginAsync();
        var seed = await SeedAsync(true, LabelType.SKU, SourceType.WORKSHOP, "WS-01");
        var first = await SubmitPrReceiptAsync(seed, "5.0000", null, new BatchPropsRequest("SHARED", null, null, null, null), Key());
        var second = await SubmitPrReceiptAsync(seed, "7.0000", first.Lines.Single().BatchId, null, Key());

        var responses = await Task.WhenAll(
            SendJsonRawAsync(HttpMethod.Post, $"/api/receipt-lines/{first.Lines.Single().Id}/quality-check", new QualityCheckRequest("PASS", "5.0000", null, null, null), Key()),
            SendJsonRawAsync(HttpMethod.Post, $"/api/receipt-lines/{second.Lines.Single().Id}/quality-check", new QualityCheckRequest("PASS", "7.0000", null, null, null), Key()));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        var inventories = await db.PhysicalInventories.Include(x => x.Subject).ToListAsync();
        Assert.Equal(0m, inventories.Single(x => x.Subject.Status == StockSubjectStatus.PENDING_INSPECTION).Quantity);
        Assert.Equal(12m, inventories.Single(x => x.Subject.Status == StockSubjectStatus.AVAILABLE).Quantity);
        Assert.Equal(4, inventories.Single(x => x.Subject.Status == StockSubjectStatus.PENDING_INSPECTION).Version);
        Assert.Equal(2, inventories.Single(x => x.Subject.Status == StockSubjectStatus.AVAILABLE).Version);

        var pendingSubjectId = inventories.Single(x => x.Subject.Status == StockSubjectStatus.PENDING_INSPECTION).SubjectId;
        var pendingMoves = await db.StockLedgers
            .Where(x => x.SubjectId == pendingSubjectId && x.Quantity < 0)
            .ToListAsync();
        Assert.Equal(2, pendingMoves.Count);
        Assert.Contains(pendingMoves, x => x.BalanceBefore == 12m);
        Assert.Contains(pendingMoves, x => x.BalanceAfter == 0m);
        Assert.All(pendingMoves, x => Assert.Equal(x.BalanceBefore + x.Quantity, x.BalanceAfter));

        var availableSubjectId = inventories.Single(x => x.Subject.Status == StockSubjectStatus.AVAILABLE).SubjectId;
        var availableMoves = await db.StockLedgers
            .Where(x => x.SubjectId == availableSubjectId && x.Quantity > 0)
            .ToListAsync();
        Assert.Equal(2, availableMoves.Count);
        Assert.Contains(availableMoves, x => x.BalanceBefore == 0m);
        Assert.Contains(availableMoves, x => x.BalanceAfter == 12m);
        Assert.All(availableMoves, x => Assert.Equal(x.BalanceBefore + x.Quantity, x.BalanceAfter));
    }

    [Fact]
    public async Task 不同key并发Resolve_一个成功另一个只返回已处理错误码()
    {
        await ResetAndLoginAsync();
        var seed = await SeedAsync(true, LabelType.SKU, SourceType.WORKSHOP, "WS-01");
        var receipt = await SubmitPrReceiptAsync(seed, "4.0000", null, new BatchPropsRequest("QC-RACE", null, null, null, null), Key());
        var photo = await UploadPhotoAsync("EXCEPTION");
        await SendJsonAsync<ReceiptItem>(
            HttpMethod.Post,
            $"/api/receipt-lines/{receipt.Lines.Single().Id}/quality-check",
            new QualityCheckRequest("EXCEPTION", "4.0000", "DAMAGED", "破损", new[] { photo.Id }),
            Key());

        Guid checkId;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
            checkId = (await db.QualityChecks.SingleAsync()).Id;
        }

        var responses = await Task.WhenAll(
            SendJsonRawAsync(HttpMethod.Post, $"/api/quality-checks/{checkId}/resolve", new ResolveQualityCheckRequest("PASS", null), Key()),
            SendJsonRawAsync(HttpMethod.Post, $"/api/quality-checks/{checkId}/resolve", new ResolveQualityCheckRequest("PASS", null), Key()));
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        var loser = Assert.Single(responses, x => x.StatusCode != HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        Assert.Equal("QUALITY_CHECK_ALREADY_RESOLVED", (await ErrorAsync(loser)).Code);
    }

    [Fact]
    public async Task 关键写接口缺IdempotencyKey_在业务执行前拒绝()
    {
        await ResetAndLoginAsync();
        var seed = await SeedAsync(false, LabelType.NONE);
        var response = await _client.PostAsJsonAsync(
            "/api/inbound-orders",
            new CreateInboundOrderRequest(
                "PO",
                seed.WarehouseId,
                "SUPPLIER",
                seed.SourceCode,
                new[] { new CreateInboundOrderLineRequest(seed.MaterialId, "1.0000") }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ErrorAsync(response)).Code);
        using var scope = _fixture.Factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AWmsDbContext>().InboundOrders.ToListAsync());
    }

    [Fact]
    public async Task 附件上传缺IdempotencyKey_在写文件和业务记录前拒绝()
    {
        await ResetAndLoginAsync();
        using var request = AttachmentUploadRequest("missing-key.png", "RECEIPT", null);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ErrorAsync(response)).Code);
        using var scope = _fixture.Factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AWmsDbContext>().Attachments.ToListAsync());
        Assert.Empty(Directory.GetFiles(_fixture.AttachmentsRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task 附件上传重复IdempotencyKey_返回首次结果且只保存一份()
    {
        await ResetAndLoginAsync();
        var key = Key();
        using var firstRequest = AttachmentUploadRequest("first.png", "RECEIPT", key);
        using var firstResponse = await _client.SendAsync(firstRequest);
        firstResponse.EnsureSuccessStatusCode();
        var first = (await firstResponse.Content.ReadFromJsonAsync<ApiResponse<AttachmentItem>>(JsonOpts))!.Data!;

        using var replayRequest = AttachmentUploadRequest("replay.png", "RECEIPT", key);
        using var replayResponse = await _client.SendAsync(replayRequest);
        replayResponse.EnsureSuccessStatusCode();
        var replay = (await replayResponse.Content.ReadFromJsonAsync<ApiResponse<AttachmentItem>>(JsonOpts))!.Data!;

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal("first.png", replay.FileName);
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Single(await db.Attachments.ToListAsync());
        Assert.Single(await db.IdempotencyRecords.Where(x => x.Key == key).ToListAsync());
        Assert.Single(Directory.GetFiles(_fixture.AttachmentsRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task 附件上传并发同IdempotencyKey_两个请求共享首次结果且只保存一份()
    {
        await ResetAndLoginAsync();
        var key = Key();
        using var firstRequest = AttachmentUploadRequest("concurrent-a.png", "RECEIPT", key);
        using var secondRequest = AttachmentUploadRequest("concurrent-b.png", "RECEIPT", key);

        var responses = await Task.WhenAll(
            _client.SendAsync(firstRequest),
            _client.SendAsync(secondRequest));
        try
        {
            Assert.All(responses, x => Assert.Equal(HttpStatusCode.Created, x.StatusCode));
            var items = await Task.WhenAll(responses.Select(async response =>
                (await response.Content.ReadFromJsonAsync<ApiResponse<AttachmentItem>>(JsonOpts))!.Data!));
            Assert.Equal(items[0].Id, items[1].Id);

            using var scope = _fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
            Assert.Single(await db.Attachments.ToListAsync());
            Assert.Single(await db.IdempotencyRecords.Where(x => x.Key == key).ToListAsync());
            Assert.Single(Directory.GetFiles(_fixture.AttachmentsRoot, "*", SearchOption.AllDirectories));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [Fact]
    public async Task 首次响应未消费后同key重试_重放首次结果且TTL后不重复入账()
    {
        await ResetAndLoginAsync();
        var seed = await SeedAsync(true, LabelType.SKU, SourceType.WORKSHOP, "WS-01");
        var key = Key();
        var body = PrReceiptRequest(seed, "6.0000", null, new BatchPropsRequest("IDEM", null, null, null, null));

        using (var request = JsonRequest(HttpMethod.Post, "/api/receipts", body, key))
        using (var ignored = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            Assert.Equal(HttpStatusCode.Created, ignored.StatusCode);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
            var record = await db.IdempotencyRecords.SingleAsync(x => x.Key == key);
            record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var replay = await SendJsonRawAsync(HttpMethod.Post, "/api/receipts", body, key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var replayReceipt = (await replay.Content.ReadFromJsonAsync<ApiResponse<ReceiptItem>>(JsonOpts))!.Data!;

        using var finalScope = _fixture.Factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(replayReceipt.Id, (await finalDb.Receipts.SingleAsync()).Id);
        Assert.Single(await finalDb.StockLedgers.ToListAsync());
    }

    [Fact]
    public async Task 已关联附件不可被无入库权限用户读取或枚举_缩略图为真实缓存图片()
    {
        await ResetAndLoginAsync();
        var seed = await SeedAsync(true, LabelType.SKU, SourceType.WORKSHOP, "WS-01");
        var photo = await UploadPhotoAsync("RECEIPT");
        await SendJsonAsync<ReceiptItem>(
            HttpMethod.Post,
            "/api/receipts",
            PrReceiptRequest(seed, "2.0000", null, new BatchPropsRequest("PHOTO", null, null, null, null), new[] { photo.Id }),
            Key());

        var thumbnails = await Task.WhenAll(
            _client.GetAsync($"/api/attachments/{photo.Id}/thumbnail"),
            _client.GetAsync($"/api/attachments/{photo.Id}/thumbnail"));
        try
        {
            Assert.All(thumbnails, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
            Assert.All(thumbnails, x => Assert.Equal("image/jpeg", x.Content.Headers.ContentType?.MediaType));
            var thumbnailBytes = await Task.WhenAll(thumbnails.Select(x => x.Content.ReadAsByteArrayAsync()));
            Assert.Equal(thumbnailBytes[0], thumbnailBytes[1]);
            Assert.True(thumbnailBytes[0].Length > 2);
            Assert.Equal(0xFF, thumbnailBytes[0][0]);
            Assert.Equal(0xD8, thumbnailBytes[0][1]);
            Assert.NotEqual(TinyPng, thumbnailBytes[0]);
            Assert.Empty(Directory.GetFiles(_fixture.AttachmentsRoot, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            foreach (var thumbnail in thumbnails)
                thumbnail.Dispose();
        }

        var unrelated = CreateClientWithPermissions("route.master-data");
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelated.GetAsync($"/api/attachments/{photo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelated.GetAsync("/api/attachments?page=1&pageSize=20")).StatusCode);

        var uploadOnly = CreateClientWithPermissions("route.inbound", "action.attachment.upload");
        using var form = new MultipartFormDataContent();
        var uploadContent = new ByteArrayContent(TinyPng);
        uploadContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(uploadContent, "file", "forbidden.png");
        form.Add(new StringContent("RECEIPT"), "bizType");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/attachments") { Content = form };
        uploadRequest.Headers.Add("Idempotency-Key", Key());
        Assert.Equal(HttpStatusCode.Forbidden, (await uploadOnly.SendAsync(uploadRequest)).StatusCode);
    }

    [Fact]
    public async Task 外部EAN_Code128_GS1_返回结构化解析与批次属性()
    {
        await ResetAndLoginAsync();
        const string ean = "4006381333931";
        await SeedAsync(true, LabelType.SKU, materialCode: ean);

        var eanResult = await ParseAsync(ean);
        Assert.Equal("EXTERNAL_BARCODE", eanResult.Type);
        Assert.Equal("EAN13", eanResult.External!.Format);
        Assert.Equal(ean, eanResult.Material!.MaterialCode);

        var code128 = await ParseAsync($"]C0{ean}");
        Assert.Equal("CODE128", code128.External!.Format);
        Assert.Equal(ean, code128.External.Parsed["code"]);

        var gs1 = await ParseAsync($"(01)0{ean}(10)LOT-01(11)260820(15)270820(30)12");
        Assert.Equal("GS1", gs1.External!.Format);
        Assert.Equal("LOT-01", gs1.BatchProps!.SourceBatchNo);
        Assert.Equal("2026-08-20", gs1.BatchProps.ProductionDate);
        Assert.Equal("2027-08-20", gs1.BatchProps.ExpiryDate);
        Assert.Equal("12.0000", gs1.Quantity);
    }

    [Fact]
    public async Task ReceiptSearch应用Filter和Sort_非法字段不静默忽略()
    {
        await ResetAndLoginAsync();
        var seed = await SeedAsync(true, LabelType.SKU, SourceType.WORKSHOP, "WS-01");
        await SendJsonAsync<ReceiptItem>(HttpMethod.Post, "/api/receipts", PrReceiptRequest(seed, "1.0000", null, new BatchPropsRequest("A", null, null, null, null), sourceDocNo: "SRC-B"), Key());
        await SendJsonAsync<ReceiptItem>(HttpMethod.Post, "/api/receipts", PrReceiptRequest(seed, "1.0000", null, new BatchPropsRequest("B", null, null, null, null), sourceDocNo: "SRC-A"), Key());

        var filtered = await SendJsonAsync<PagedResult<ReceiptItem>>(
            HttpMethod.Post,
            "/api/receipts/search",
            new ReceiptSearchRequest(
                null,
                null,
                null,
                null,
                null,
                new FilterGroup("and", new List<FilterCondition> { new("sourceDocNo", "eq", "SRC-A") }),
                new[] { new SortOption("receiptNo", "asc") },
                1,
                20),
            null);
        Assert.Single(filtered.Items);
        Assert.Equal("SRC-A", filtered.Items[0].SourceDocNo);

        var invalid = await _client.PostAsJsonAsync(
            "/api/receipts/search",
            new ReceiptSearchRequest(
                null,
                null,
                null,
                null,
                null,
                new FilterGroup("and", new List<FilterCondition> { new("notAllowed", "eq", "x") }),
                null,
                1,
                20));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ErrorAsync(invalid)).Code);
    }

    [Fact]
    public async Task 打印失败保留FAILED_条件Retry原子落盘并生成含二维码的可靠PDF()
    {
        await ResetAndLoginAsync();
        var seed = await SeedAsync(false, LabelType.NONE);
        var order = await CreatePoOrderAsync(seed, "1.0000", Key());

        Directory.Delete(_fixture.PrintRoot, recursive: true);
        await File.WriteAllTextAsync(_fixture.PrintRoot, "block directory creation");
        var failed = await SendJsonAsync<PrintJobDto>(
            HttpMethod.Post,
            "/api/print/inbound-order-qr",
            new InboundOrderQrPrintRequest(order.Id),
            Key());
        Assert.Equal("FAILED", failed.Status);
        Assert.Equal("PRINT_GENERATION_FAILED", failed.ErrorCode);
        Assert.Null(failed.FileUrl);

        File.Delete(_fixture.PrintRoot);
        Directory.CreateDirectory(_fixture.PrintRoot);
        var ready = await SendJsonAsync<PrintJobDto>(
            HttpMethod.Post,
            $"/api/print/jobs/{failed.Id}/retry",
            new { },
            Key());
        Assert.Equal(failed.Id, ready.Id);
        Assert.Equal("READY", ready.Status);
        Assert.NotNull(ready.FileUrl);

        var file = await _client.GetAsync($"/api/print/jobs/{ready.Id}/file");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        var pdf = await file.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-1.7", Encoding.ASCII.GetString(pdf));
        var pdfText = Encoding.ASCII.GetString(pdf);
        Assert.Contains("/Subtype /Image", pdfText);
        Assert.Contains("/QR", pdfText);
        Assert.Equal(ready.Items.Single().Content, DecodeQrFromPdf(pdf));
        var startXrefMarker = pdfText.LastIndexOf("startxref\n", StringComparison.Ordinal);
        var xrefValueStart = startXrefMarker + "startxref\n".Length;
        var xrefValueEnd = pdfText.IndexOf('\n', xrefValueStart);
        var xrefOffset = int.Parse(pdfText[xrefValueStart..xrefValueEnd]);
        Assert.StartsWith("xref", pdfText[xrefOffset..]);
        Assert.Empty(Directory.GetFiles(_fixture.PrintRoot, "*.tmp"));

        var invalidRetry = await SendJsonRawAsync(HttpMethod.Post, $"/api/print/jobs/{ready.Id}/retry", new { }, Key());
        Assert.Equal(HttpStatusCode.Conflict, invalidRetry.StatusCode);
        Assert.Equal("PRINT_JOB_STATUS_INVALID", (await ErrorAsync(invalidRetry)).Code);
    }

    private async Task ResetAndLoginAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiTestFixture.AdminUsername,
            password = ApiTestFixture.AdminPassword
        });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope!.Data!.Token);
    }

    private async Task<SeedData> SeedAsync(
        bool batchControlled,
        LabelType labelType,
        SourceType sourceType = SourceType.SUPPLIER,
        string sourceCode = "SUP-001",
        string? materialCode = null)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        var suffix = Guid.CreateVersion7().ToString("N")[..8];
        var warehouse = new Warehouse { Code = $"WH-{suffix}", Name = "一号仓", Status = MaterialStatus.ENABLED };
        var material = new Material
        {
            Code = materialCode ?? $"MAT-{suffix}",
            Name = "测试物料",
            BatchControlled = batchControlled,
            LabelType = labelType,
            DefaultUom = "CT",
            DefaultQtyPerLabel = 5,
            Status = MaterialStatus.ENABLED
        };
        var source = new Source { Type = sourceType, Code = sourceCode, Name = "测试来源", Status = MaterialStatus.ENABLED };
        db.AddRange(warehouse, material, source);
        await db.SaveChangesAsync();
        var staging = new Location { WarehouseId = warehouse.Id, Code = $"STG-{suffix}", Type = LocationType.STAGING, Status = MaterialStatus.ENABLED };
        var target = new Location { WarehouseId = warehouse.Id, Code = $"DFT-{suffix}", Type = LocationType.DEFAULT, Status = MaterialStatus.ENABLED };
        db.AddRange(staging, target);
        await db.SaveChangesAsync();
        return new SeedData(warehouse.Id, staging.Id, target.Id, target.Code, material.Id, sourceCode);
    }

    private Task<InboundOrderItem> CreatePoOrderAsync(SeedData seed, string expectedQty, string key) =>
        SendJsonAsync<InboundOrderItem>(
            HttpMethod.Post,
            "/api/inbound-orders",
            new CreateInboundOrderRequest(
                "PO",
                seed.WarehouseId,
                "SUPPLIER",
                seed.SourceCode,
                new[] { new CreateInboundOrderLineRequest(seed.MaterialId, expectedQty) }),
            key);

    private Task<ReceiptItem> SubmitPrReceiptAsync(
        SeedData seed,
        string quantity,
        Guid? batchId,
        BatchPropsRequest? batchProps,
        string key) =>
        SendJsonAsync<ReceiptItem>(HttpMethod.Post, "/api/receipts", PrReceiptRequest(seed, quantity, batchId, batchProps), key);

    private static SubmitReceiptRequest PrReceiptRequest(
        SeedData seed,
        string quantity,
        Guid? batchId,
        BatchPropsRequest? batchProps,
        IReadOnlyList<Guid>? photos = null,
        string? sourceDocNo = null) =>
        new(
            seed.WarehouseId,
            seed.StagingLocationId,
            null,
            "PR",
            sourceDocNo,
            "WORKSHOP",
            seed.SourceCode,
            new[] { new SubmitReceiptLineRequest(null, seed.MaterialId, batchId, batchProps, quantity, null) },
            photos);

    private async Task<AttachmentItem> UploadPhotoAsync(string bizType)
    {
        using var request = AttachmentUploadRequest("photo.png", bizType, Key());
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AttachmentItem>>(JsonOpts))!.Data!;
    }

    private static HttpRequestMessage AttachmentUploadRequest(string fileName, string bizType, string? key)
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(TinyPng);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "file", fileName);
        form.Add(new StringContent(bizType), "bizType");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/attachments") { Content = form };
        if (key != null)
            request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private async Task<ScanResult> ParseAsync(string content)
    {
        var response = await _client.PostAsJsonAsync("/api/scan/parse", new ScanParseRequest(content, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ScanResult>>(JsonOpts))!.Data!;
    }

    private HttpClient CreateClientWithPermissions(params string[] permissions)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<TokenService>();
        var user = new User { Id = Guid.CreateVersion7(), Username = "unrelated", Name = "无关用户" };
        var token = tokenService.GenerateAccessToken(user, permissions).Token;
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string path, object body, string? key)
    {
        var response = key == null
            ? await _client.SendAsync(new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) })
            : await SendJsonRawAsync(method, path, body, key);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOpts))!.Data!;
    }

    private Task<HttpResponseMessage> SendJsonRawAsync(HttpMethod method, string path, object body, string key) =>
        _client.SendAsync(JsonRequest(method, path, body, key));

    private static HttpRequestMessage JsonRequest(HttpMethod method, string path, object body, string key)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static async Task<ApiResponse<object>> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts))!;

    private static string Key() => Guid.CreateVersion7().ToString("N");

    private static byte[] CreateTinyPng()
    {
        using var image = new Image<Rgba32>(2, 2, new Rgba32(30, 120, 210));
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static string DecodeQrFromPdf(byte[] pdf)
    {
        var imageMarker = Encoding.ASCII.GetBytes("/Subtype /Image");
        var markerIndex = pdf.AsSpan().IndexOf(imageMarker);
        Assert.True(markerIndex >= 0);
        var streamMarker = Encoding.ASCII.GetBytes("stream\n");
        var relativeStreamIndex = pdf.AsSpan(markerIndex).IndexOf(streamMarker);
        Assert.True(relativeStreamIndex >= 0);
        var streamIndex = markerIndex + relativeStreamIndex;
        var dictionary = Encoding.ASCII.GetString(pdf, markerIndex, streamIndex - markerIndex);
        var width = int.Parse(Regex.Match(dictionary, @"/Width (\d+)").Groups[1].Value);
        var height = int.Parse(Regex.Match(dictionary, @"/Height (\d+)").Groups[1].Value);
        var length = int.Parse(Regex.Match(dictionary, @"/Length (\d+)").Groups[1].Value);
        var dataStart = streamIndex + streamMarker.Length;

        using var compressed = new MemoryStream(pdf, dataStart, length, writable: false);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var pixels = raw.ToArray();
        Assert.Equal(width * height, pixels.Length);

        const int scale = 8;
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        var rendered = new byte[renderedWidth * renderedHeight];
        for (var y = 0; y < renderedHeight; y++)
        {
            for (var x = 0; x < renderedWidth; x++)
                rendered[y * renderedWidth + x] = pixels[(y / scale) * width + x / scale];
        }

        var reader = new BarcodeReaderGeneric
        {
            Options =
            {
                PureBarcode = true,
                TryHarder = true,
                PossibleFormats = [BarcodeFormat.QR_CODE]
            }
        };
        var result = reader.Decode(rendered, renderedWidth, renderedHeight, RGBLuminanceSource.BitmapFormat.Gray8);
        return Assert.IsType<Result>(result).Text;
    }

    private sealed record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);
    private sealed record SeedData(Guid WarehouseId, Guid StagingLocationId, Guid DefaultLocationId, string DefaultLocationCode, Guid MaterialId, string SourceCode);
}
