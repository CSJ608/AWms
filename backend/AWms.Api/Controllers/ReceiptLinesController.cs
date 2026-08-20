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
[Route("api/receipt-lines")]
public class ReceiptLinesController : ControllerBase
{
    private readonly ReceiptService _service;

    public ReceiptLinesController(ReceiptService service) => _service = service;

    [HttpPost("{lineId:guid}/quality-check")]
    [RequirePermission("action.quality.check")]
    public async Task<ActionResult<ApiResponse<ReceiptItem>>> QualityCheck(Guid lineId, [FromBody] QualityCheckRequest request, CancellationToken ct)
    {
        var result = await _service.SubmitQualityCheckAsync(lineId, request, User.UserId(), User.UserDisplayName(), ct);
        return Ok(ApiResponse.Ok(result));
    }
}
