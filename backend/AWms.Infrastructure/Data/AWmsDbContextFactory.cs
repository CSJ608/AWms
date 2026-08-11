using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AWms.Infrastructure.Data;

/// <summary>设计时工厂：供 dotnet ef 生成/管理迁移使用（连接串仅占位，不参与迁移生成）。</summary>
public class AWmsDbContextFactory : IDesignTimeDbContextFactory<AWmsDbContext>
{
    public AWmsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseNpgsql("Host=localhost;Database=awms_design;Username=postgres;Password=placeholder", npgsql =>
            {
                npgsql.MigrationsAssembly("AWms.Infrastructure");
            })
            .Options;
        return new AWmsDbContext(options);
    }
}
