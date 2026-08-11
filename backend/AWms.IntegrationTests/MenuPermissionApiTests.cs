using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Domain.Dtos.Auth;
using AWms.Domain.Dtos.Common;

namespace AWms.IntegrationTests;

/// <summary>
/// C4① 菜单权限过滤：/auth/me 返回的 menus 按角色权限过滤
/// （OPERATOR 仅 inbound；SUPERVISOR 无 system；SYSTEM_ADMIN 全量 4 项）。
/// </summary>
public class MenuPermissionApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _adminClient;

    public MenuPermissionApiTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _adminClient = fixture.Factory.CreateClient();
    }

    private async Task LoginAdminAsync()
    {
        var resp = await _adminClient.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiTestFixture.AdminUsername,
            password = ApiTestFixture.AdminPassword
        });
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope!.Data!.Token);
    }

    private async Task<HttpClient> CreateUserAndLoginAsync(string roleCode)
    {
        var roleId = await GetRoleIdAsync(roleCode);
        var username = $"{roleCode.ToLowerInvariant()}-{Guid.CreateVersion7().ToString("N")[..8]}";
        var created = await _adminClient.PostAsJsonAsync("/api/users", new
        {
            username,
            name = roleCode,
            password = "Pass123!",
            roleIds = new[] { roleId }
        });
        created.EnsureSuccessStatusCode();

        var login = await _adminClient.PostAsJsonAsync("/api/auth/login", new { username, password = "Pass123!" });
        login.EnsureSuccessStatusCode();
        var envelope = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope!.Data!.Token);
        return client;
    }

    private async Task<Guid> GetRoleIdAsync(string code)
    {
        var resp = await _adminClient.GetAsync("/api/roles");
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<List<RoleItem>>>(JsonOpts);
        return envelope!.Data!.Single(r => r.Code == code).Id;
    }

    private static async Task<LoginResponse> GetMeAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/auth/me");
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        return envelope!.Data!;
    }

    [Fact]
    public async Task OPERATOR_Me_菜单仅dashboard与inbound_pda含receiving()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var op = await CreateUserAndLoginAsync("OPERATOR");

        var me = await GetMeAsync(op);

        Assert.Equal(new[] { "menu.dashboard", "menu.inbound" }, me.Menus.Web.Select(m => m.Code).OrderBy(c => c).ToArray());
        Assert.DoesNotContain(me.Menus.Web, m => m.Code is "menu.master-data" or "menu.system");
        Assert.Contains(me.Menus.Pda, m => m.Code == "pda.receiving");
    }

    [Fact]
    public async Task SUPERVISOR_Me_菜单含dashboard与inbound与masterdata_不含system()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var sv = await CreateUserAndLoginAsync("SUPERVISOR");

        var me = await GetMeAsync(sv);

        Assert.Equal(new[] { "menu.dashboard", "menu.inbound", "menu.master-data" }, me.Menus.Web.Select(m => m.Code).OrderBy(c => c).ToArray());
        Assert.DoesNotContain(me.Menus.Web, m => m.Code == "menu.system");
    }

    [Fact]
    public async Task SYSTEM_ADMIN_Me_菜单全量4项()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();

        var me = await GetMeAsync(_adminClient);

        Assert.Equal(
            new[] { "menu.dashboard", "menu.inbound", "menu.master-data", "menu.system" },
            me.Menus.Web.Select(m => m.Code).OrderBy(c => c).ToArray());
        Assert.Contains(me.Menus.Pda, m => m.Code == "pda.receiving");
    }

    private record RoleItem(Guid Id, string Code, string Name, List<string> PermissionCodes, DateTime CreatedAt);
}
