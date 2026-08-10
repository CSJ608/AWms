using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AWms.Api.Middleware;
using AWms.Domain.Dtos.Roles;
using AWms.Domain.Dtos.Common;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly AuthService _authService;

    public RolesController(AuthService authService) => _authService = authService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RoleItem>>>> List([FromQuery] string? keyword)
    {
        var result = await _authService.ListRolesAsync(keyword);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost]
    [RequirePermission("action.user.manage")]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var result = await _authService.CreateRoleAsync(request);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var role = await _authService.GetRoleAsync(id);
        return Ok(ApiResponse.Ok(role));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("action.user.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var result = await _authService.UpdateRoleAsync(id, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}/permissions")]
    [RequirePermission("action.user.manage")]
    public async Task<IActionResult> AssignPermissions(Guid id, [FromBody] AssignPermissionsRequest request)
    {
        var result = await _authService.AssignPermissionsAsync(id, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("action.user.manage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _authService.DeleteRoleAsync(id);
        return NoContent();
    }
}
