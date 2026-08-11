using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Domain.Dtos.Common;

namespace AWms.IntegrationTests;

/// <summary>
/// Q4 契约测试：GET /api/meta/fields/users 运行时元数据端点（认证权限.md 声明）。
/// </summary>
public class MetaApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public MetaApiTests(ApiTestFixture fixture)
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

    [Fact]
    public async Task MetaFields_Users_返回字段元数据()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();

        var resp = await _client.GetAsync("/api/meta/fields/users");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<List<FieldMeta>>>(JsonOpts);
        var fields = envelope!.Data!;
        var byName = fields.ToDictionary(f => f.Field);

        Assert.Contains("username", byName.Keys);
        Assert.Contains("name", byName.Keys);
        Assert.Contains("status", byName.Keys);
        Assert.Contains("createdAt", byName.Keys);

        Assert.Equal("string", byName["username"].Type);
        Assert.Contains("startsWith", byName["username"].Operators);

        Assert.Equal("enum", byName["status"].Type);
        Assert.Equal(new[] { "eq", "in" }, byName["status"].Operators);
        Assert.Contains(byName["status"].Options!, o => o.Value == "ACTIVE");
        Assert.Contains(byName["status"].Options!, o => o.Value == "DISABLED");

        Assert.Equal("datetime", byName["createdAt"].Type);
        Assert.Contains("between", byName["createdAt"].Operators);
    }

    [Fact]
    public async Task MetaFields_未知资源_404()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();

        var resp = await _client.GetAsync("/api/meta/fields/not-a-resource");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("NOT_FOUND", envelope!.Code);
    }

    private record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);
    private record FieldMeta(string Field, string LabelKey, string Type, List<string> Operators, string? RefResource, List<FieldOption>? Options);
    private record FieldOption(string Value, string LabelKey);
}