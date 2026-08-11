using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AWms.Domain.Dtos.Auth;
using AWms.Domain.Dtos.Common;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService) => _authService = authService;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<RefreshResponse>>> Refresh()
    {
        var token = Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
            return Unauthorized(ApiResponse.Error<object>("UNAUTHORIZED", "缺少 token"));

        var result = await _authService.RefreshAsync(token);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        // 契约允许无状态 logout（复验意见）：仅返回 204
        await _authService.LogoutAsync(Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "") ?? "");
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> GetMe()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(ApiResponse.Error<object>("UNAUTHORIZED", "无效 token"));

        var token = Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "") ?? string.Empty;
        var result = await _authService.GetMeAsync(userId, token);
        return Ok(ApiResponse.Ok(result));
    }
}

