using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Configurations;

namespace AWms.IntegrationTests;

/// <summary>PostgreSQL Testcontainer fixture：每个测试类集合一个容器；启动即应用全部迁移（规范 §5.4）。</summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    static PostgreSqlFixture()
    {
        // 本环境 Docker Hub 直连不可达（仅镜像源可用），禁 Ryuk 以免拉取 testcontainers/ryuk 失败；
        // 容器生命周期由本 fixture DisposeAsync 显式回收，功能等价。
        TestcontainersSettings.ResourceReaperEnabled = false;
    }

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("awms_test")
        .WithUsername("awms")
        .WithPassword("awms_test_pw")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsAssembly("AWms.Infrastructure"))
            .Options;
        await using var db = new AWmsDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("PostgreSql")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}
