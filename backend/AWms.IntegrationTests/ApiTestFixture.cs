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

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(),
        "awms-api-tests",
        Guid.CreateVersion7().ToString("N"));

    public string AttachmentsRoot => Path.Combine(_storageRoot, "attachments");
    public string PrintRoot => Path.Combine(_storageRoot, "print-jobs");

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>当前夹具容器的连接串（供同库独立 WebApplicationFactory 复用，如 Q6 仅 AWMS_JWT_SECRET 测试）。</summary>
    public string ContainerConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(AttachmentsRoot);
        Directory.CreateDirectory(PrintRoot);
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
                builder.UseSetting("Storage:AttachmentsRoot", AttachmentsRoot);
                builder.UseSetting("Storage:PrintRoot", PrintRoot);
            });
        // 触发应用启动（迁移 + 初始管理员）
        _ = Factory.Server;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }

    public async Task ResetDatabaseAsync()
    {
        // 每个测试前清空业务表，保持隔离（种子/权限等基础数据保留）
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AWmsDbContext>();
        db.StockLedgers.RemoveRange(db.StockLedgers);
        db.TxnGroups.RemoveRange(db.TxnGroups);
        db.PutawayRecords.RemoveRange(db.PutawayRecords);
        db.QualityChecks.RemoveRange(db.QualityChecks);
        db.ReceiptLines.RemoveRange(db.ReceiptLines);
        db.Receipts.RemoveRange(db.Receipts);
        db.UniqueCodes.RemoveRange(db.UniqueCodes);
        db.InboundOrderLines.RemoveRange(db.InboundOrderLines);
        db.InboundOrders.RemoveRange(db.InboundOrders);
        db.PhysicalInventories.RemoveRange(db.PhysicalInventories);
        db.StockSubjects.RemoveRange(db.StockSubjects);
        db.PrintJobItems.RemoveRange(db.PrintJobItems);
        db.PrintJobs.RemoveRange(db.PrintJobs);
        db.Attachments.RemoveRange(db.Attachments);
        db.Locations.RemoveRange(db.Locations);
        db.Materials.RemoveRange(db.Materials);
        db.Warehouses.RemoveRange(db.Warehouses);
        db.Sources.RemoveRange(db.Sources);
        db.Batches.RemoveRange(db.Batches);
        db.ImportTasks.RemoveRange(db.ImportTasks);
        db.IdempotencyRecords.RemoveRange(db.IdempotencyRecords);
        await db.SaveChangesAsync();

        ResetDirectory(AttachmentsRoot);
        ResetDirectory(PrintRoot);
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        else if (File.Exists(path))
            File.Delete(path);
        Directory.CreateDirectory(path);
    }
}
