using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AWms.Tests.Services;

public class AdminSeedServiceTests
{
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"H:{password}";
        public bool Verify(string password, string hash) => hash == $"H:{password}";
    }

    private static AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AWmsDbContext(options);
        db.Roles.Add(new Role { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "SYSTEM_ADMIN", Name = "系统管理员" });
        db.SaveChanges();
        return db;
    }

    private static AdminSeedService CreateService(AWmsDbContext db, AdminSeedOptions options) =>
        new(db, new FakePasswordHasher(), Options.Create(options), NullLogger<AdminSeedService>.Instance);

    [Fact]
    public async Task EnsureInitialAdminAsync_未配置密码_抛配置异常()
    {
        var db = CreateDb();
        var service = CreateService(db, new AdminSeedOptions { Password = null });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureInitialAdminAsync());
        Assert.Contains("AWMS_ADMIN_PASSWORD", ex.Message);
    }

    [Fact]
    public async Task EnsureInitialAdminAsync_无管理员_创建并绑定SYSTEM_ADMIN()
    {
        var db = CreateDb();
        var service = CreateService(db, new AdminSeedOptions { Username = "admin", Password = "P@ssw0rd-2026", Name = "系统管理员" });

        await service.EnsureInitialAdminAsync();

        var admin = await db.Users.Include(u => u.UserRoles).SingleAsync(u => u.Username == "admin");
        Assert.Equal(UserStatus.ACTIVE, admin.Status);
        Assert.Equal("H:P@ssw0rd-2026", admin.PasswordHash);
        var role = Assert.Single(admin.UserRoles.Select(ur => ur.RoleId));
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), role);
    }

    [Fact]
    public async Task EnsureInitialAdminAsync_已存在且不重置_密码保持不变()
    {
        var db = CreateDb();
        db.Users.Add(new User { Username = "admin", Name = "原管理员", PasswordHash = "H:old-password", Status = UserStatus.ACTIVE });
        await db.SaveChangesAsync();
        var service = CreateService(db, new AdminSeedOptions { Username = "admin", Password = "new-password", ResetOnStartup = false });

        await service.EnsureInitialAdminAsync();

        var admin = await db.Users.SingleAsync(u => u.Username == "admin");
        Assert.Equal("H:old-password", admin.PasswordHash);
    }

    [Fact]
    public async Task EnsureInitialAdminAsync_已存在且首启重置_密码更新为新密码()
    {
        var db = CreateDb();
        db.Users.Add(new User { Username = "admin", Name = "原管理员", PasswordHash = "H:old-password", Status = UserStatus.ACTIVE });
        await db.SaveChangesAsync();
        var service = CreateService(db, new AdminSeedOptions { Username = "admin", Password = "new-password", ResetOnStartup = true });

        await service.EnsureInitialAdminAsync();

        var admin = await db.Users.SingleAsync(u => u.Username == "admin");
        Assert.Equal("H:new-password", admin.PasswordHash);
    }

    [Fact]
    public async Task EnsureInitialAdminAsync_角色不存在_抛配置异常()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AWmsDbContext(options); // 无种子角色
        var service = CreateService(db, new AdminSeedOptions { Username = "admin", Password = "x" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureInitialAdminAsync());
        Assert.Contains("SYSTEM_ADMIN", ex.Message);
    }
}

