using AWms.Domain.Dtos.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AWms.Api.Middleware;

/// <summary>
/// 操作级权限过滤（复验意见：RequirePermission 必须落地，不只定义）。
/// 校验 JWT 中的 permission claim；无权限返回 403 FORBIDDEN envelope。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            return;

        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(ApiResponse.Error<object>("UNAUTHORIZED", "未登录或登录已过期"));
            return;
        }

        var hasPermission = user.Claims.Any(c => c.Type == "permission" && c.Value == _permission);
        if (!hasPermission)
        {
            context.Result = new ObjectResult(ApiResponse.Error<object>("FORBIDDEN", $"缺少操作权限：{_permission}"))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
