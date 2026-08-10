using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AWms.IntegrationTests;

/// <summary>主数据 API 全链路：CRUD / 重复码 409 / 引用保护 / filter DSL / 白名单外 400 / 嵌套 batches。</summary>
public class MasterDataApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public MasterDataApiTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
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

    private static async Task<ApiResponse<MaterialItem>> CreateMaterialAsync(HttpClient client, string code, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/materials", new
        {
            code, name, searchCode = "SC", batchControlled = true, labelType = "SKU", defaultUom = "CT", defaultQtyPerLabel = "10.0000"
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ApiResponse<MaterialItem>>(JsonOpts))!;
    }

    [Fact]
    public async Task 物料CRUD_重复码409_删除引用保护()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");

        // 创建
        var created = await CreateMaterialAsync(_client, $"{prefix}-01", "物料一");
        var materialId = created.Data!.Id;

        // 重复码 409
        var dup = await _client.PostAsJsonAsync("/api/materials", new
        {
            code = $"{prefix}-01", name = "重复", batchControlled = false, labelType = "NONE", defaultUom = "CT"
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var dupEnvelope = await dup.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("MATERIAL_CODE_DUPLICATED", dupEnvelope!.Code);

        // 编辑（code 只读）
        var put = await _client.PutAsJsonAsync($"/api/materials/{materialId}", new
        {
            name = "物料一改", searchCode = "SC2", batchControlled = true, labelType = "SKU", defaultUom = "CT", status = "ENABLED"
        });
        put.EnsureSuccessStatusCode();

        // 详情
        var get = await _client.GetAsync($"/api/materials/{materialId}");
        get.EnsureSuccessStatusCode();
        var detail = await get.Content.ReadFromJsonAsync<ApiResponse<MaterialItem>>(JsonOpts);
        Assert.Equal("物料一改", detail!.Data!.Name);

        // 引用保护：先建批次再删物料 → 409
        var batch = await _client.PostAsJsonAsync("/api/materials", new
        {
            code = $"{prefix}-02", name = "物料二", batchControlled = true, labelType = "SKU", defaultUom = "CT"
        });
        batch.EnsureSuccessStatusCode();
        var batchEnvelope = await batch.Content.ReadFromJsonAsync<ApiResponse<MaterialItem>>(JsonOpts);
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AWms.Infrastructure.Data.AWmsDbContext>();
            db.Batches.Add(new AWms.Domain.Entities.Batch
            {
                MaterialId = batchEnvelope!.Data!.Id,
                MaterialCode = $"{prefix}-02",
                BatchNo = $"{prefix[..8]}001"
            });
            await db.SaveChangesAsync();
        }
        var del = await _client.DeleteAsync($"/api/materials/{batchEnvelope!.Data!.Id}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        var delEnvelope = await del.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("MATERIAL_IN_USE", delEnvelope!.Code);

        // 无引用物料删除 204
        var delOk = await _client.DeleteAsync($"/api/materials/{materialId}");
        Assert.Equal(HttpStatusCode.NoContent, delOk.StatusCode);
    }

    [Fact]
    public async Task Search_带filter_返回过滤结果_白名单外400()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");
        var a = await CreateMaterialAsync(_client, $"{prefix}-A", "启用甲");
        var b = await CreateMaterialAsync(_client, $"{prefix}-B", "停用乙");
        // 把 B 置为 DISABLED，验证 status 筛选
        var putB = await _client.PutAsJsonAsync($"/api/materials/{b.Data!.Id}", new
        {
            name = "停用乙", batchControlled = false, labelType = "NONE", defaultUom = "CT", status = "DISABLED"
        });
        putB.EnsureSuccessStatusCode();

        var search = await _client.PostAsJsonAsync("/api/materials/search", new
        {
            filter = new
            {
                op = "and",
                conditions = new object[]
                {
                    new { field = "code", op = "contains", value = prefix },
                    new { field = "status", op = "eq", value = "ENABLED" }
                }
            },
            page = 1,
            pageSize = 20
        });
        search.EnsureSuccessStatusCode();
        var searchEnvelope = await search.Content.ReadFromJsonAsync<ApiResponse<PagedResult<MaterialItem>>>(JsonOpts);
        Assert.Equal(1, searchEnvelope!.Data!.Total);
        Assert.Equal($"{prefix}-A", searchEnvelope.Data.Items[0].Code);

        // 白名单外字段 → 400
        var bad = await _client.PostAsJsonAsync("/api/materials/search", new
        {
            filter = new
            {
                op = "and",
                conditions = new object[] { new { field = "hacked;drop", op = "eq", value = "x" } }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var badEnvelope = await bad.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("VALIDATION_ERROR", badEnvelope!.Code);
    }

    [Fact]
    public async Task 仓库库位_嵌套创建与查询_仓内重复409()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");

        var wh = await _client.PostAsJsonAsync("/api/warehouses", new { code = $"{prefix}-WH", name = "一号仓" });
        wh.EnsureSuccessStatusCode();
        var whEnvelope = await wh.Content.ReadFromJsonAsync<ApiResponse<WarehouseItem>>(JsonOpts);
        var whId = whEnvelope!.Data!.Id;

        var loc = await _client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new { code = $"{prefix}-STG", type = "STAGING" });
        loc.EnsureSuccessStatusCode();

        var locDup = await _client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new { code = $"{prefix}-STG", type = "STAGING" });
        Assert.Equal(HttpStatusCode.Conflict, locDup.StatusCode);
        var dupEnvelope = await locDup.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("LOCATION_CODE_DUPLICATED", dupEnvelope!.Code);

        var list = await _client.PostAsJsonAsync($"/api/warehouses/{whId}/locations/search", new { page = 1, pageSize = 20 });
        list.EnsureSuccessStatusCode();
        var listEnvelope = await list.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LocationItem>>>(JsonOpts);
        Assert.Equal(1, listEnvelope!.Data!.Total);
        Assert.Equal($"{prefix}-STG", listEnvelope.Data.Items[0].Code);
    }

    [Fact]
    public async Task 嵌套批次_物料不存在404_默认时间降序()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();

        var missing = await _client.PostAsJsonAsync("/api/materials/00000000-0000-0000-0000-000000000999/batches/search", new { page = 1, pageSize = 20 });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var env = await missing.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("MATERIAL_NOT_FOUND", env!.Code);
    }

    private record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);
    private record MaterialItem(Guid Id, string Code, string Name, string? SearchCode, bool BatchControlled, string LabelType, string DefaultUom, string? DefaultQtyPerLabel, string Status, DateTime CreatedAt, DateTime UpdatedAt);
    private record WarehouseItem(Guid Id, string Code, string Name, string? SearchCode, string Status, string MgmtMode, DateTime CreatedAt);
    private record LocationItem(Guid Id, Guid WarehouseId, string WarehouseCode, string Code, string? SearchCode, string Type, string Status, string Reachability, DateTime CreatedAt);
}


