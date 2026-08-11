using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Api;
using AWms.Domain.Dtos.Common;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AWms.IntegrationTests;

/// <summary>
/// Q6 契约测试：JWT 密钥单一来源。
/// - 仅注入 AWMS_JWT_SECRET（不设 Jwt:SecretKey）即可登录并访问受保护端点（不再出现 IDX10703）；
/// - 未配置密钥启动即抛错（原 Program 校验逻辑保留）。
/// </summary>
public class JwtSecretSourceTests : IClassFixture<ApiTestFixture>
{
    private const string EnvOnlySecret = "EnvOnlyJwtSecret-2026-MustBeAtLeast32Chars!!";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;

    public JwtSecretSourceTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task 仅注入AWMS_JWT_SECRET_登录200并可访问受保护端点()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Default", _fixture.ContainerConnectionString);
                // 只注入 AWMS_JWT_SECRET，绝不设置 Jwt:SecretKey
                builder.UseSetting("AWMS_JWT_SECRET", EnvOnlySecret);
                builder.UseSetting("Admin:Username", ApiTestFixture.AdminUsername);
                builder.UseSetting("Admin:Password", ApiTestFixture.AdminPassword);
                builder.UseSetting("Admin:Name", "系统管理员");
                builder.UseSetting("Admin:RoleCode", "SYSTEM_ADMIN");
                builder.UseSetting("Admin:ResetOnStartup", "false");
            });
        // 触发应用启动（迁移 + 初始管理员）
        _ = factory.Server;

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiTestFixture.AdminUsername,
            password = ApiTestFixture.AdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var envelope = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        Assert.False(string.IsNullOrEmpty(envelope!.Data!.Token));

        // 访问受保护端点（认证 + 权限过滤均基于同一密钥签发的 token）
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", envelope.Data.Token);
        var me = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public void 未配置密钥_启动即抛错()
    {
        using var factory = new WebApplicationFactory<Program>();
        var ex = Assert.ThrowsAny<Exception>(() => _ = factory.Server);
        Assert.Contains("未配置", InnermostMessage(ex));
    }

    private static string InnermostMessage(Exception ex)
    {
        while (ex.InnerException is not null)
            ex = ex.InnerException;
        return ex.Message;
    }

    private record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);
}