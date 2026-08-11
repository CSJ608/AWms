using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AWms.Api.Middleware;
using AWms.Domain.Dtos.Users;
using AWms.Domain.Dtos.Common;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/users")]
[RequirePermission("route.system")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AuthService _authService;

    public UsersController(AuthService authService) => _authService = authService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UserItem>>>> List(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // sort 形如 "name:desc" 或 "name"（契约：排序白名单 username/name/status/createdAt，默认 username asc）
        string? sortField = null;
        string? sortDir = null;
        if (!string.IsNullOrWhiteSpace(sort))
        {
            var parts = sort.Split(':');
            sortField = parts[0];
            sortDir = parts.Length > 1 ? parts[1] : "asc";
        }
        var result = await _authService.ListUsersAsync(keyword, status, sortField, sortDir, page, pageSize);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost]
    [RequirePermission("action.user.manage")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var result = await _authService.CreateUserAsync(request);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var user = await _authService.GetUserAsync(id);
        return Ok(ApiResponse.Ok(user));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("action.user.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var result = await _authService.UpdateUserAsync(id, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}/roles")]
    [RequirePermission("action.user.manage")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request)
    {
        var result = await _authService.AssignRolesAsync(id, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission("action.user.manage")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(id, request.NewPassword);
        return NoContent();
    }
}
