using Microsoft.EntityFrameworkCore;
using AWms.Domain.Dtos.Auth;
using AWms.Domain.Dtos.Users;
using AWms.Domain.Dtos.Roles;
using AWms.Domain.Dtos.Permissions;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;

namespace AWms.Infrastructure.Services;

public class AuthService
{
    private readonly AWmsDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TokenService _tokenService;

    public AuthService(AWmsDbContext db, IPasswordHasher passwordHasher, TokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var user = await LoadUserWithPermissionsAsync(u => u.Username == username);

        if (user == null)
            throw new DomainException("LOGIN_FAILED", "用户名或密码错误", 401);

        if (user.Status == UserStatus.DISABLED)
            throw new DomainException("USER_DISABLED", "账号已停用", 401);

        if (!_passwordHasher.Verify(password, user.PasswordHash))
            throw new DomainException("LOGIN_FAILED", "用户名或密码错误", 401);

        var permissions = ExtractPermissions(user);
        var (token, expiresAt) = _tokenService.GenerateAccessToken(user, permissions);

        return BuildLoginResponse(user, token, expiresAt, permissions);
    }

    /// <summary>refresh：用过期 token 换新（签名有效即可，生命周期放行）。</summary>
    public async Task<RefreshResponse> RefreshAsync(string expiredToken)
    {
        if (!_tokenService.ValidateExpiredToken(expiredToken, out var userId, out _))
            throw new DomainException("UNAUTHORIZED", "Token无效", 401);

        var user = await LoadUserWithPermissionsAsync(u => u.Id == userId);
        if (user == null || user.Status == UserStatus.DISABLED)
            throw new DomainException("UNAUTHORIZED", "用户不存在或已停用", 401);

        var permissions = ExtractPermissions(user);
        var (newToken, expiresAt) = _tokenService.GenerateAccessToken(user, permissions);

        return new RefreshResponse(newToken, expiresAt);
    }

    /// <summary>logout：契约允许无状态实现（复验意见），返回 204 即可。</summary>
    public Task LogoutAsync(string token) => Task.CompletedTask;

    /// <summary>me：返回 LoginResponse 同构（不重新签发 token，复验意见 P2）。</summary>
    public async Task<LoginResponse> GetMeAsync(Guid userId, string currentToken)
    {
        var user = await LoadUserWithPermissionsAsync(u => u.Id == userId)
            ?? throw new DomainException("USER_NOT_FOUND", "用户不存在", 404);

        var permissions = ExtractPermissions(user);
        var expiresAt = _tokenService.GetTokenExpiry(currentToken) ?? DateTime.UtcNow.AddHours(2);

        return BuildLoginResponse(user, currentToken, expiresAt, permissions);
    }

    private async Task<User?> LoadUserWithPermissionsAsync(System.Linq.Expressions.Expression<Func<User, bool>> predicate) =>
        await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate);

    private static List<string> ExtractPermissions(User user) =>
        user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

    private LoginResponse BuildLoginResponse(User user, string token, DateTime expiresAt, List<string> permissions)
    {
        var roles = user.UserRoles.Select(ur => new RoleBrief(ur.Role.Id, ur.Role.Code, ur.Role.Name)).ToList();
        return new LoginResponse(
            token,
            expiresAt,
            new UserInfo(user.Id, user.Username, user.Name,
                user.Status == UserStatus.ACTIVE ? "ACTIVE" : "DISABLED", roles),
            permissions,
            BuildMenus(permissions));
    }

    private MenuGroup BuildMenus(IReadOnlyList<string> permissions)
    {
        var allMenus = _db.MenuDefinitions.AsNoTracking().ToList();

        var web = BuildMenuTree(allMenus.Where(m => m.Surface == Surface.WEB).ToList(), null, permissions);
        var pda = BuildMenuTree(allMenus.Where(m => m.Surface == Surface.PDA).ToList(), null, permissions);

        return new MenuGroup(web, pda);
    }

    private static List<MenuDto> BuildMenuTree(List<MenuDefinition> all, Guid? parentId, IReadOnlyList<string> permissions)
    {
        return all
            .Where(m => m.ParentId == parentId)
            .OrderBy(m => m.Sort)
            .Select(m =>
            {
                var children = BuildMenuTree(all, m.Id, permissions);
                if (!string.IsNullOrEmpty(m.RequiredPermissionCode) && !permissions.Contains(m.RequiredPermissionCode))
                    return null;

                return new MenuDto(
                    m.Code, m.TitleKey, m.GroupKey, m.ModuleCode,
                    m.IconKey, m.Path, m.Sort,
                    children.Count > 0 ? children : null);
            })
            .Where(x => x != null)
            .Cast<MenuDto>()
            .ToList();
    }

    // ---- Users ----
    public async Task<PagedResult<UserItem>> ListUsersAsync(string? keyword, string? status, string? sortField, string? sortDir, int page, int pageSize)
    {
        var query = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(u => u.Username.Contains(keyword) || u.Name.Contains(keyword));

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<UserStatus>(status, true, out var statusEnum))
                throw new DomainException("VALIDATION_ERROR", $"status 值无效：{status}", 400);
            query = query.Where(u => u.Status == statusEnum);
        }

        var allowed = new HashSet<string> { "username", "name", "status", "createdAt" };
        var field = string.IsNullOrWhiteSpace(sortField) ? "username" : sortField;
        var dir = string.IsNullOrWhiteSpace(sortDir) ? "asc" : sortDir;
        if (!allowed.Contains(field))
            throw new DomainException("VALIDATION_ERROR", $"Sort field '{field}' not allowed", 400);
        if (dir != "asc" && dir != "desc")
            throw new DomainException("VALIDATION_ERROR", $"Sort dir '{dir}' must be asc/desc", 400);

        query = (field, dir) switch
        {
            ("username", "desc") => query.OrderByDescending(u => u.Username).ThenByDescending(u => u.Id),
            ("username", _) => query.OrderBy(u => u.Username).ThenBy(u => u.Id),
            ("name", "desc") => query.OrderByDescending(u => u.Name).ThenByDescending(u => u.Id),
            ("name", _) => query.OrderBy(u => u.Name).ThenBy(u => u.Id),
            ("status", "desc") => query.OrderByDescending(u => u.Status).ThenByDescending(u => u.Id),
            ("status", _) => query.OrderBy(u => u.Status).ThenBy(u => u.Id),
            ("createdAt", "desc") => query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id),
            _ => query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var result = items.Select(ToUserItem).ToList();
        return new PagedResult<UserItem>(result.AsReadOnly(), total, page, pageSize);
    }

    public async Task<UserItem> GetUserAsync(Guid id)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new DomainException("USER_NOT_FOUND", "用户不存在", 404);
        return ToUserItem(user);
    }

    public async Task<UserItem> CreateUserAsync(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Password))
            throw new DomainException("VALIDATION_ERROR", "用户名/姓名/密码必填", 400);

        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            throw new DomainException("USERNAME_DUPLICATED", "用户名已存在", 409);

        var user = new User
        {
            Username = request.Username,
            Name = request.Name,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Status = request.Status == "DISABLED" ? UserStatus.DISABLED : UserStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (request.RoleIds != null && request.RoleIds.Count > 0)
        {
            var roles = await _db.Roles.Where(r => request.RoleIds.Contains(r.Id)).ToListAsync();
            foreach (var role in roles)
            {
                user.UserRoles.Add(new UserRole { User = user, Role = role });
            }
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return ToUserItem(user);
    }

    public async Task<UserItem> UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new DomainException("USER_NOT_FOUND", "用户不存在", 404);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "姓名必填", 400);

        user.Name = request.Name;
        user.Status = request.Status == "DISABLED" ? UserStatus.DISABLED : UserStatus.ACTIVE;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return ToUserItem(user);
    }

    public async Task<UserItem> AssignRolesAsync(Guid userId, AssignRolesRequest request)
    {
        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new DomainException("USER_NOT_FOUND", "用户不存在", 404);

        _db.UserRoles.RemoveRange(user.UserRoles);

        if (request.RoleIds.Count > 0)
        {
            var roles = await _db.Roles.Where(r => request.RoleIds.Contains(r.Id)).ToListAsync();
            foreach (var role in roles)
            {
                _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
            }
        }

        await _db.SaveChangesAsync();

        var updated = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstAsync(u => u.Id == userId);
        return ToUserItem(updated);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new DomainException("VALIDATION_ERROR", "新密码必填", 400);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new DomainException("USER_NOT_FOUND", "用户不存在", 404);

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ---- Roles ----
    public async Task<List<RoleItem>> ListRolesAsync(string? keyword)
    {
        var query = _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(r => r.Code.Contains(keyword) || r.Name.Contains(keyword));

        var roles = await query.OrderBy(r => r.Code).ToListAsync();

        return roles.Select(ToRoleItem).ToList();
    }

    public async Task<RoleItem> GetRoleAsync(Guid id)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new DomainException("ROLE_NOT_FOUND", "角色不存在", 404);
        return ToRoleItem(role);
    }

    public async Task<RoleItem> CreateRoleAsync(CreateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "角色编码/名称必填", 400);

        if (await _db.Roles.AnyAsync(r => r.Code == request.Code))
            throw new DomainException("ROLE_CODE_DUPLICATED", "角色编码已存在", 409);

        var role = new Role { Code = request.Code, Name = request.Name, CreatedAt = DateTime.UtcNow };

        if (request.PermissionCodes != null)
        {
            var perms = await _db.Permissions.Where(p => request.PermissionCodes.Contains(p.Code)).ToListAsync();
            foreach (var p in perms)
                role.RolePermissions.Add(new RolePermission { Role = role, Permission = p });
        }

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return ToRoleItem(role);
    }

    public async Task<RoleItem> UpdateRoleAsync(Guid id, UpdateRoleRequest request)
    {
        var role = await _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new DomainException("ROLE_NOT_FOUND", "角色不存在", 404);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "角色名称必填", 400);

        role.Name = request.Name;

        if (request.PermissionCodes != null)
        {
            _db.RolePermissions.RemoveRange(role.RolePermissions);
            var perms = await _db.Permissions.Where(p => request.PermissionCodes.Contains(p.Code)).ToListAsync();
            foreach (var p in perms)
                _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p.Id });
        }

        await _db.SaveChangesAsync();

        return ToRoleItem(role);
    }

    public async Task<RoleItem> AssignPermissionsAsync(Guid roleId, AssignPermissionsRequest request)
    {
        return await UpdateRoleAsync(roleId, new UpdateRoleRequest(string.Empty, request.PermissionCodes));
    }

    public async Task DeleteRoleAsync(Guid id)
    {
        var role = await _db.Roles.Include(r => r.UserRoles).FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new DomainException("ROLE_NOT_FOUND", "角色不存在", 404);

        if (role.UserRoles.Count > 0)
            throw new DomainException("ROLE_IN_USE", "角色被用户引用，禁止删除", 409);

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
    }

    // ---- Permissions ----
    public async Task<List<PermissionItem>> ListPermissionsAsync()
    {
        var perms = await _db.Permissions.AsNoTracking().OrderBy(p => p.ModuleCode).ThenBy(p => p.Category).ToListAsync();
        return perms.Select(p => new PermissionItem(p.Id, p.Code, p.Name, p.Category.ToString(), p.ModuleCode)).ToList();
    }

    private static UserItem ToUserItem(User u) =>
        new(u.Id, u.Username, u.Name,
            u.Status == UserStatus.ACTIVE ? "ACTIVE" : "DISABLED",
            u.UserRoles.Select(ur => new UserRoleDto(ur.Role.Id, ur.Role.Code, ur.Role.Name)).ToList(),
            u.CreatedAt);

    private static RoleItem ToRoleItem(Role r) =>
        new(r.Id, r.Code, r.Name,
            r.RolePermissions.Select(rp => rp.Permission.Code).Distinct().ToList(),
            r.CreatedAt);
}

