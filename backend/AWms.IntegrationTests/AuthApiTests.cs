using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Api;
using AWms.Infrastructure.Services;
using AWms.Domain.Dtos.Auth;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace AWms.IntegrationTests;

/// <summary>认证全链路 API 测试：登录/错误/停用/refresh过期换新/me/logout/权限过滤。</summary>
public class AuthApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public AuthApiTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new { username, password });
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        return envelope!.Data!;
    }

    [Fact]
    public async Task Login_初始管理员_返回契约形状()
    {
        await _fixture.ResetDatabaseAsync();
        var result = await LoginAsync(ApiTestFixture.AdminUsername, ApiTestFixture.AdminPassword);

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(ApiTestFixture.AdminUsername, result.User.Username);
        Assert.NotEmpty(result.User.Roles);
        Assert.Contains(result.Permissions, p => p == "action.material.create");
        Assert.NotNull(result.Menus);
        Assert.NotNull(result.Menus.Web);
        Assert.NotNull(result.Menus.Pda);
        Assert.Contains(result.Menus.Web, m => m.Code == "menu.dashboard");
    }

    [Fact]
    public async Task Login_密码错误_401_LOGIN_FAILED()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new { username = ApiTestFixture.AdminUsername, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("LOGIN_FAILED", envelope!.Code);
    }

    [Fact]
    public async Task Login_停用用户_401_USER_DISABLED()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await LoginAsync(ApiTestFixture.AdminUsername, ApiTestFixture.AdminPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);

        var created = await _client.PostAsJsonAsync("/api/users", new { username = "disabled01", name = "停用账号", password = "Pass123!" });
        created.EnsureSuccessStatusCode();
        var createdBody = await created.Content.ReadFromJsonAsync<ApiResponse<UserItem>>(JsonOpts);
        var userId = createdBody!.Data!.Id;
        var put = await _client.PutAsJsonAsync($"/api/users/{userId}", new { name = "停用账号", status = "DISABLED" });
        put.EnsureSuccessStatusCode();
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync("/api/auth/login", new { username = "disabled01", password = "Pass123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("USER_DISABLED", envelope!.Code);
    }

    [Fact]
    public async Task Refresh_过期token_换新成功()
    {
        await _fixture.ResetDatabaseAsync();
        // 用相同密钥/签发者生成“真实过期 token”（负有效期）
        var options = Options.Create(new TokenService.JwtOptions
        {
            SecretKey = ApiTestFixture.JwtSecret,
            Issuer = "AWms",
            Audience = "AWms",
            AccessTokenExpiry = TimeSpan.FromHours(-2)
        });
        var tokenService = new TokenService(options);
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        var admin = await db.Users.AsNoTracking().FirstAsync(u => u.Username == ApiTestFixture.AdminUsername);
        var (expiredToken, expiredAt) = tokenService.GenerateAccessToken(admin, new List<string>());
        Assert.True(expiredAt < DateTime.UtcNow);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<RefreshResponse>>(JsonOpts);
        Assert.NotNull(envelope!.Data);
        Assert.NotEqual(expiredToken, envelope.Data.Token);
        Assert.True(envelope.Data.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Refresh_无效token_401()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Me_返回同构且token不变()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await LoginAsync(ApiTestFixture.AdminUsername, ApiTestFixture.AdminPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);

        var resp = await _client.GetAsync("/api/auth/me");

        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        Assert.Equal(admin.Token, envelope!.Data!.Token); // 不重新签发
        Assert.Equal(admin.User.Id, envelope.Data.User.Id);
    }

    [Fact]
    public async Task Logout_204()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await LoginAsync(ApiTestFixture.AdminUsername, ApiTestFixture.AdminPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);

        var resp = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task RequirePermission_无权限用户写物料_403_FORBIDDEN()
    {
        await _fixture.ResetDatabaseAsync();
        var admin = await LoginAsync(ApiTestFixture.AdminUsername, ApiTestFixture.AdminPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);

        // 建 OPERATOR 用户（仅入库权限）
        var created = await _client.PostAsJsonAsync("/api/users", new
        {
            username = "operator01",
            name = "作业员",
            password = "Pass123!",
            roleIds = new[] { (await GetRoleIdAsync("OPERATOR")) }
        });
        created.EnsureSuccessStatusCode();

        var op = await LoginAsync("operator01", "Pass123!");
        var opClient = _fixture.Factory.CreateClient();
        opClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", op.Token);

        var resp = await opClient.PostAsJsonAsync("/api/materials", new
        {
            code = "MAT-FB-001", name = "越权物料", batchControlled = false, labelType = "NONE", defaultUom = "CT"
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("FORBIDDEN", envelope!.Code);
    }

    private async Task<Guid> GetRoleIdAsync(string code)
    {
        var resp = await _client.GetAsync("/api/roles");
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<List<RoleItem>>>(JsonOpts);
        return envelope!.Data!.Single(r => r.Code == code).Id;
    }

    private record RoleItem(Guid Id, string Code, string Name, List<string> PermissionCodes, DateTime CreatedAt);
    private record UserItem(Guid Id, string Username, string Name, string Status, List<object> Roles, DateTime CreatedAt);
}

