using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AWms.Api.Middleware;
using AWms.Domain.Dtos.Permissions;
using AWms.Domain.Dtos.Common;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[RequirePermission("route.system")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly AuthService _authService;

    public PermissionsController(AuthService authService) => _authService = authService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PermissionItem>>>> List()
    {
        var result = await _authService.ListPermissionsAsync();
        return Ok(ApiResponse.Ok(result));
    }
}
