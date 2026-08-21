using AWms.Api;
using AWms.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Configurations;

namespace AWms.IntegrationTests;

/// <summary>
/// API 集成测试夹具：WebApplicationFactory + Testcontainers PostgreSQL。
/// 启动即应用全部迁移 + 初始管理员（Program 启动逻辑，B-01/B-02 验证）。
/// </summary>
public sealed class ApiTestFixture : IAsyncLifetime
{
    static ApiTestFixture()
    {
        // 与 PostgreSqlFixture 相同的受限 registry 处理：禁 Ryuk，容器由 DisposeAsync 回收
        TestcontainersSettings.ResourceReaperEnabled = false;
    }

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("awms_api_test")
        .WithUsername("awms")
        .WithPassword("awms_test_pw")
        .Build();

    public const string AdminUsername = "admin";
    public const string AdminPassword = "ApiTest-P@ssw0rd-2026";
    public const string JwtSecret = "ApiTestSecretKey-MustBeAtLeast32Chars!-2026";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>当前夹具容器的连接串（供同库独立 WebApplicationFactory 复用，如 Q6 仅 AWMS_JWT_SECRET 测试）。</summary>
    public string ContainerConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Default", _container.GetConnectionString());
                builder.UseSetting("Jwt:SecretKey", JwtSecret);
                builder.UseSetting("Jwt:Issuer", "AWms");
                builder.UseSetting("Jwt:Audience", "AWms");
                builder.UseSetting("Jwt:AccessTokenExpiry", "02:00:00");
                builder.UseSetting("Admin:Username", AdminUsername);
                builder.UseSetting("Admin:Password", AdminPassword);
                builder.UseSetting("Admin:Name", "系统管理员");
                builder.UseSetting("Admin:RoleCode", "SYSTEM_ADMIN");
                builder.UseSetting("Admin:ResetOnStartup", "false");
            });
        // 触发应用启动（迁移 + 初始管理员）
        _ = Factory.Server;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        // 每个测试前清空业务表，保持隔离（种子/权限等基础数据保留）
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        db.Materials.RemoveRange(db.Materials);
        db.Warehouses.RemoveRange(db.Warehouses);
        db.Locations.RemoveRange(db.Locations);
        db.Sources.RemoveRange(db.Sources);
        db.Batches.RemoveRange(db.Batches);
        db.ImportTasks.RemoveRange(db.ImportTasks);
        db.IdempotencyRecords.RemoveRange(db.IdempotencyRecords);
        await db.SaveChangesAsync();
    }
}
