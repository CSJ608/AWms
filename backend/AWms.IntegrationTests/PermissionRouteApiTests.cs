using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Domain.Dtos.Common;

namespace AWms.IntegrationTests;

/// <summary>
/// Q2/Q3 权限测试：读端点按资源挂 route 权限；仓库/库位/来源操作权限码注册与种子；
/// SUPERVISOR 可写主数据、OPERATOR 不可读系统/主数据也不可写仓库来源。
/// </summary>
public class PermissionRouteApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] NewActionCodes =
    {
        "action.warehouse.create", "action.warehouse.edit", "action.warehouse.delete",
        "action.location.create", "action.location.edit", "action.location.delete",
        "action.source.create", "action.source.edit", "action.source.delete"
    };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public PermissionRouteApiTests(ApiTestFixture fixture)
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

    private async Task<HttpClient> CreateUserAndLoginAsync(string username, string roleCode)
    {
        var roleId = await GetRoleIdAsync(roleCode);
        var created = await _client.PostAsJsonAsync("/api/users", new
        {
            username,
            name = roleCode,
            password = "Pass123!",
            roleIds = new[] { roleId }
        });
        created.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { username, password = "Pass123!" });
        login.EnsureSuccessStatusCode();
        var envelope = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        var userClient = _fixture.Factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope!.Data!.Token);
        return userClient;
    }

    private async Task<Guid> GetRoleIdAsync(string code)
    {
        var resp = await _client.GetAsync("/api/roles");
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<List<RoleItem>>>(JsonOpts);
        return envelope!.Data!.Single(r => r.Code == code).Id;
    }

    [Fact]
    public async Task 权限注册表_含仓库库位来源9个新码()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();

        var resp = await _client.GetAsync("/api/permissions");
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<List<PermissionItem>>>(JsonOpts);
        var codes = envelope!.Data!.Select(p => p.Code).ToHashSet();

        Assert.All(NewActionCodes, code => Assert.Contains(code, codes));
    }

    [Fact]
    public async Task SUPERVISOR_写仓库与来源_200()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N")[..8];
        var supervisor = await CreateUserAndLoginAsync($"sv-{prefix}", "SUPERVISOR");

        var wh = await supervisor.PostAsJsonAsync("/api/warehouses", new { code = $"{prefix}-WH", name = "仓管仓" });
        Assert.Equal(HttpStatusCode.Created, wh.StatusCode);

        var loc = await supervisor.PostAsJsonAsync($"/api/warehouses/{await GetIdAsync(wh)}/locations", new { code = $"{prefix}-STG", type = "STAGING" });
        Assert.Equal(HttpStatusCode.Created, loc.StatusCode);

        var src = await supervisor.PostAsJsonAsync("/api/sources", new { type = "SUPPLIER", code = $"{prefix}-SP", name = "仓管来源" });
        Assert.Equal(HttpStatusCode.Created, src.StatusCode);

        var search = await supervisor.PostAsJsonAsync("/api/materials/search", new { page = 1, pageSize = 10 });
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        // SUPERVISOR 无 route.system：读系统端点 403
        var users = await supervisor.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
    }

    [Fact]
    public async Task OPERATOR_读系统与主数据_写仓库来源_403()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N")[..8];
        var op = await CreateUserAndLoginAsync($"op-{prefix}", "OPERATOR");

        var users = await op.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);

        var search = await op.PostAsJsonAsync("/api/materials/search", new { page = 1, pageSize = 10 });
        Assert.Equal(HttpStatusCode.Forbidden, search.StatusCode);

        var wh = await op.PostAsJsonAsync("/api/warehouses", new { code = $"{prefix}-WH", name = "越权仓" });
        Assert.Equal(HttpStatusCode.Forbidden, wh.StatusCode);

        var src = await op.PostAsJsonAsync("/api/sources", new { type = "SUPPLIER", code = $"{prefix}-SP", name = "越权来源" });
        Assert.Equal(HttpStatusCode.Forbidden, src.StatusCode);
    }

    [Fact]
    public async Task admin_读写全部_200()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N")[..8];

        var users = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);

        var search = await _client.PostAsJsonAsync("/api/materials/search", new { page = 1, pageSize = 10 });
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var wh = await _client.PostAsJsonAsync("/api/warehouses", new { code = $"{prefix}-WH", name = "管理仓" });
        Assert.Equal(HttpStatusCode.Created, wh.StatusCode);

        var src = await _client.PostAsJsonAsync("/api/sources", new { type = "SUPPLIER", code = $"{prefix}-SP", name = "管理来源" });
        Assert.Equal(HttpStatusCode.Created, src.StatusCode);
    }

    private static async Task<Guid> GetIdAsync(HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);
    private record RoleItem(Guid Id, string Code, string Name, List<string> PermissionCodes, DateTime CreatedAt);
    private record PermissionItem(Guid Id, string Code, string Name, string Category, string ModuleCode);
}