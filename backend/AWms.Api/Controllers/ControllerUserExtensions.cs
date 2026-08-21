using System.Security.Claims;

namespace AWms.Api.Controllers;

internal static class ControllerUserExtensions
{
    public static Guid UserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static string UserDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name) ?? "未知用户";
}
