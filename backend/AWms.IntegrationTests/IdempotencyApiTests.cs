using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AWms.IntegrationTests;

/// <summary>幂等 API：同 key 重复写返回首次结果；并发同 key 仅执行一次；错误响应也缓存；TTL 过期可重放。</summary>
public class IdempotencyApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public IdempotencyApiTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task LoginAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new { username = ApiTestFixture.AdminUsername, password = ApiTestFixture.AdminPassword });
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope!.Data!.Token);
    }

    private HttpRequestMessage CreateMaterialRequest(string code, string idemKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/materials")
        {
            Content = JsonContent.Create(new
            {
                code,
                name = "幂等物料",
                batchControlled = false,
                labelType = "NONE",
                defaultUom = "CT"
            })
        };
        req.Headers.Add("Idempotency-Key", idemKey);
        return req;
    }

    [Fact]
    public async Task 同key重复写_返回首次结果_仅入库一次()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");
        var key = $"mat-{prefix}";

        var first = await _client.SendAsync(CreateMaterialRequest($"{prefix}-01", key));
        var second = await _client.SendAsync(CreateMaterialRequest($"{prefix}-01", key));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstBody = await first.Content.ReadAsStringAsync();
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.Equal(firstBody, secondBody);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(1, await db.Materials.CountAsync(m => m.Code == $"{prefix}-01"));
    }

    [Fact]
    public async Task 并发同key_仅执行一次_全部返回相同结果()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");
        var key = $"concurrent-{prefix}";

        var tasks = Enumerable.Range(0, 8).Select(_ => _client.SendAsync(CreateMaterialRequest($"{prefix}-c", key))).ToArray();
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var bodies = (await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()))).Distinct().ToList();
        Assert.Single(bodies);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        Assert.Equal(1, await db.Materials.CountAsync(m => m.Code == $"{prefix}-c"));
    }

    [Fact]
    public async Task 错误响应_同key重试_返回首次错误()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");

        // 首次创建成功
        var first = await _client.SendAsync(CreateMaterialRequest($"{prefix}-01", "err-key"));
        first.EnsureSuccessStatusCode();

        // 重复码请求 + key：首次 409 被缓存
        var dup1 = await _client.SendAsync(CreateMaterialRequest($"{prefix}-01", "dup-err-key"));
        Assert.Equal(HttpStatusCode.Conflict, dup1.StatusCode);
        var dup2 = await _client.SendAsync(CreateMaterialRequest($"{prefix}-01", "dup-err-key"));
        Assert.Equal(HttpStatusCode.Conflict, dup2.StatusCode);
        var b1 = await dup1.Content.ReadAsStringAsync();
        var b2 = await dup2.Content.ReadAsStringAsync();
        Assert.Equal(b1, b2);
        Assert.Contains("MATERIAL_CODE_DUPLICATED", b2);
    }

    [Fact]
    public async Task TTL过期_可重放()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");
        var key = $"ttl-{prefix}";

        var first = await _client.SendAsync(CreateMaterialRequest($"{prefix}-01", key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // 直接把记录置为过期（模拟 24h 后）
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
            var record = await db.IdempotencyRecords.SingleAsync(r => r.Key == key);
            record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        // 重放：新 key 状态允许再执行 → 201（同 code 唯一冲突会 409，因此用新 code 验证“可重放”）
        var replay = await _client.SendAsync(CreateMaterialRequest($"{prefix}-02", key));
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
    }
    private record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);
}

