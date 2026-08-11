using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Auth;
using AWms.Domain.Dtos.Users;
using AWms.Domain.Dtos.Roles;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AWms.Tests.Services;

public class AuthServiceTests
{
    private static AWmsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AWmsDbContext(options);
    }

    private static AuthService CreateAuthService(AWmsDbContext db)
    {
        var passwordHasher = new Argon2PasswordHasher();
        var tokenService = CreateTokenService();
        return new AuthService(db, passwordHasher, tokenService);
    }

    private static TokenService CreateTokenService()
    {
        var options = Options.Create(new TokenService.JwtOptions
        {
            SecretKey = "TestSecretKey-MustBeAtLeast32Chars!",
            Issuer = "AWms",
            Audience = "AWms",
            AccessTokenExpiry = TimeSpan.FromHours(2)
        });
        return new TokenService(options);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsLoginResponseWithContractShape()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        var hash = hasher.Hash("test123");

        var role = new Role { Id = Guid.NewGuid(), Code = "OPERATOR", Name = "作业员" };
        var permission = new Permission { Id = Guid.NewGuid(), Code = "route.inbound", Name = "入库", Category = PermissionCategory.ROUTE, ModuleCode = "inbound" };
        db.Roles.Add(role);
        db.Permissions.Add(permission);
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Name = "测试",
            PasswordHash = hash,
            Status = UserStatus.ACTIVE,
            UserRoles = { new UserRole { RoleId = role.Id } }
        });
        db.MenuDefinitions.Add(new MenuDefinition
        {
            Code = "menu.dashboard",
            TitleKey = "nav.workspace",
            GroupKey = "nav.group.workspace",
            ModuleCode = "dashboard",
            IconKey = "home",
            Path = "/",
            Surface = Surface.WEB,
            Sort = 10
        });
        await db.SaveChangesAsync();

        // Detach all tracked entities to avoid identity conflicts
        db.ChangeTracker.Clear();

        var service = CreateAuthService(db);

        var result = await service.LoginAsync("testuser", "test123");

        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(result.User);
        Assert.Equal("testuser", result.User.Username);
        Assert.Equal("测试", result.User.Name);
        Assert.Equal("ACTIVE", result.User.Status);
        Assert.Single(result.User.Roles);
        Assert.Equal("OPERATOR", result.User.Roles[0].Code);
        Assert.Contains("route.inbound", result.Permissions);
        // menus.web must exist per contract
        Assert.NotNull(result.Menus);
        Assert.NotNull(result.Menus.Web);
        Assert.NotNull(result.Menus.Pda);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsLoginFailed()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        db.Users.Add(new User { Username = "testuser", Name = "测试", PasswordHash = hasher.Hash("correct") });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.LoginAsync("testuser", "wrong"));
        Assert.Equal("LOGIN_FAILED", ex.Code);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_DisabledUser_ThrowsUserDisabled()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        db.Users.Add(new User { Username = "testuser", Name = "测试", PasswordHash = hasher.Hash("test"), Status = UserStatus.DISABLED });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.LoginAsync("testuser", "test"));
        Assert.Equal("USER_DISABLED", ex.Code);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ReturnsNewToken()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        var tokenService = CreateTokenService();
        var user = new User { Id = Guid.NewGuid(), Username = "testuser", Name = "测试", PasswordHash = hasher.Hash("test"), Status = UserStatus.ACTIVE };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new AuthService(db, hasher, tokenService);
        var (expiredToken, _) = tokenService.GenerateAccessToken(user, new List<string> { "route.inbound" });

        var result = await service.RefreshAsync(expiredToken);

        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
        Assert.NotEqual(expiredToken, result.Token);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_Throws409()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        db.Users.Add(new User { Username = "testuser", Name = "测试", PasswordHash = hasher.Hash("test") });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var request = new CreateUserRequest("testuser", "新用户", "pass123", null, null);
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateUserAsync(request));
        Assert.Equal("USERNAME_DUPLICATED", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateRoleAsync_DuplicateCode_Throws409()
    {
        var db = CreateDbContext();
        db.Roles.Add(new Role { Code = "OPERATOR", Name = "作业员" });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateRoleAsync(new("OPERATOR", "重复", null)));
        Assert.Equal("ROLE_CODE_DUPLICATED", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteRoleAsync_RoleInUse_Throws409()
    {
        var db = CreateDbContext();
        var role = new Role { Code = "ADMIN", Name = "管理员" };
        role.UserRoles.Add(new UserRole { User = new User { Username = "admin", Name = "管理员", PasswordHash = "x" } });
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DeleteRoleAsync(role.Id));
        Assert.Equal("ROLE_IN_USE", ex.Code);
        Assert.Equal(409, ex.StatusCode);
}

    [Fact]
    public async Task UpdateUserAsync_改名改状态_生效()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        var user = new User { Id = Guid.NewGuid(), Username = "u1", Name = "原名", PasswordHash = hasher.Hash("x"), Status = UserStatus.ACTIVE };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var result = await service.UpdateUserAsync(user.Id, new("新名", "DISABLED"));

        Assert.Equal("新名", result.Name);
        Assert.Equal("DISABLED", result.Status);
        var reloaded = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(UserStatus.DISABLED, reloaded.Status);
    }

    [Fact]
    public async Task AssignRolesAsync_替换角色_生效()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        var user = new User { Id = Guid.NewGuid(), Username = "u1", Name = "名", PasswordHash = hasher.Hash("x") };
        var role1 = new Role { Code = "R1", Name = "角色一" };
        var role2 = new Role { Code = "R2", Name = "角色二" };
        db.Users.Add(user);
        db.Roles.AddRange(role1, role2);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var result = await service.AssignRolesAsync(user.Id, new AssignRolesRequest(new List<Guid> { role1.Id, role2.Id }));

        Assert.Equal(2, result.Roles.Count);
    }

    [Fact]
    public async Task ResetPasswordAsync_新密码可登录()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        var user = new User { Id = Guid.NewGuid(), Username = "u1", Name = "名", PasswordHash = hasher.Hash("old") };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        await service.ResetPasswordAsync(user.Id, "new-pass");

        var reloaded = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.True(hasher.Verify("new-pass", reloaded.PasswordHash));
    }

    [Fact]
    public async Task UpdateRoleAsync_改名与权限_生效()
    {
        var db = CreateDbContext();
        var role = new Role { Code = "R1", Name = "旧名" };
        var perm = new Permission { Code = "route.inbound", Name = "入库", Category = PermissionCategory.ROUTE, ModuleCode = "inbound" };
        db.Roles.Add(role);
        db.Permissions.Add(perm);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var result = await service.UpdateRoleAsync(role.Id, new("新名", new List<string> { "route.inbound" }));

        Assert.Equal("新名", result.Name);
        Assert.Contains("route.inbound", result.PermissionCodes);
    }

    [Fact]
    public async Task AssignPermissionsAsync_更新权限点_生效()
    {
        var db = CreateDbContext();
        var role = new Role { Code = "R1", Name = "角色" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var result = await service.AssignPermissionsAsync(role.Id, new AssignPermissionsRequest(new List<string>()));

        Assert.Empty(result.PermissionCodes);
    }

    [Fact]
    public async Task ListUsersAsync_分页与默认排序_生效()
    {
        var db = CreateDbContext();
        var hasher = new Argon2PasswordHasher();
        db.Users.AddRange(
            new User { Username = "b_user", Name = "乙", PasswordHash = hasher.Hash("x") },
            new User { Username = "a_user", Name = "甲", PasswordHash = hasher.Hash("x") });
        await db.SaveChangesAsync();
        var service = CreateAuthService(db);

        var result = await service.ListUsersAsync(null, null, null, null, 1, 10);

        Assert.Equal(2, result.Total);
        Assert.Equal("a_user", result.Items[0].Username); // 默认 username asc
    }

    [Fact]
    public async Task GetRoleAsync_不存在_抛404()
    {
        var db = CreateDbContext();
        var service = CreateAuthService(db);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.GetRoleAsync(Guid.NewGuid()));
        Assert.Equal("ROLE_NOT_FOUND", ex.Code);
        Assert.Equal(404, ex.StatusCode);
    }
}

