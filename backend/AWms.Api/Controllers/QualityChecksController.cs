using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Receipts;
using AWms.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AWms.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission("route.inbound")]
[Route("api/quality-checks")]
public class QualityChecksController : ControllerBase
{
    private readonly ReceiptService _service;

    public QualityChecksController(ReceiptService service) => _service = service;

    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<QualityExceptionItem>>>> Search([FromBody] QualityExceptionSearchRequest request, CancellationToken ct)
    {
        var result = await _service.SearchQualityExceptionsAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("{checkId:guid}/resolve")]
    [RequirePermission("action.quality.resolve")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<ReceiptItem>>> Resolve(Guid checkId, [FromBody] ResolveQualityCheckRequest request, CancellationToken ct)
    {
        var result = await _service.ResolveQualityCheckAsync(checkId, request, User.UserId(), User.UserDisplayName(), ct);
        return Ok(ApiResponse.Ok(result));
    }
}
