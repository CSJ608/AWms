using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AWms.Infrastructure.Services;

/// <summary>初始管理员配置（环境变量注入，密钥不进 git）。</summary>
public class AdminSeedOptions
{
    /// <summary>管理员用户名（环境变量 AWMS_ADMIN_USERNAME，默认 admin）。</summary>
    public string Username { get; set; } = "admin";
    /// <summary>管理员初始密码（环境变量 AWMS_ADMIN_PASSWORD，必填，无默认值）。</summary>
    public string? Password { get; set; }
    /// <summary>管理员显示名。</summary>
    public string Name { get; set; } = "系统管理员";
    /// <summary>初始角色编码。</summary>
    public string RoleCode { get; set; } = "SYSTEM_ADMIN";
    /// <summary>首启重置：为 true 时每次启动用环境变量密码重置管理员密码（规范 §2.5）。</summary>
    public bool ResetOnStartup { get; set; }
}

/// <summary>
/// 初始管理员初始化：保证新库可登录。
/// 规则（规范 §2.5 + 复验意见 B-02）：
/// 1. 管理员密码由环境变量注入（AWMS_ADMIN_PASSWORD），不落 git；
/// 2. 管理员不存在则创建并绑定 SYSTEM_ADMIN 角色；
/// 3. ResetOnStartup=true 时每次启动重置密码（首启重置），并有测试。
/// </summary>
public partial class AdminSeedService
{
    private readonly AWmsDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AdminSeedOptions _options;
    private readonly ILogger<AdminSeedService> _logger;

    public AdminSeedService(AWmsDbContext db, IPasswordHasher passwordHasher, IOptions<AdminSeedOptions> options, ILogger<AdminSeedService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureInitialAdminAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Password))
            throw new InvalidOperationException("Admin:Password (AWMS_ADMIN_PASSWORD) 未配置：初始管理员密码必须通过环境变量注入，禁止写入 git。");

        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Code == _options.RoleCode, cancellationToken)
            ?? throw new InvalidOperationException($"初始角色 {_options.RoleCode} 不存在，请检查种子数据。");

        var admin = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == _options.Username, cancellationToken);

        if (admin is null)
        {
            admin = new User
            {
                Username = _options.Username,
                Name = _options.Name,
                PasswordHash = _passwordHasher.Hash(_options.Password),
                Status = UserStatus.ACTIVE,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Users.Add(admin);
            // 用导航属性建立关联，避免 SaveChanges 前取不到生成的 Id
            _db.UserRoles.Add(new UserRole { User = admin, RoleId = role.Id });
            LogAdminCreated(_options.Username, _options.RoleCode);
        }
        else if (_options.ResetOnStartup)
        {
            admin.PasswordHash = _passwordHasher.Hash(_options.Password);
            admin.UpdatedAt = DateTime.UtcNow;
            LogAdminPasswordReset(_options.Username);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public partial class AdminSeedService
{
    [Microsoft.Extensions.Logging.LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "初始管理员 {Username} 已创建并绑定角色 {RoleCode}")]
    private partial void LogAdminCreated(string username, string roleCode);

    [Microsoft.Extensions.Logging.LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "初始管理员 {Username} 已按首启重置更新密码")]
    private partial void LogAdminPasswordReset(string username);
}
