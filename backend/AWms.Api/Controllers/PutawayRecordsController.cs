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
[Route("api/putaway-records")]
public class PutawayRecordsController : ControllerBase
{
    private readonly ReceiptService _service;

    public PutawayRecordsController(ReceiptService service) => _service = service;

    [HttpPost]
    [RequirePermission("action.putaway.create")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<ReceiptItem>>> Create([FromBody] CreatePutawayRecordRequest request, CancellationToken ct)
    {
        var result = await _service.CreatePutawayRecordAsync(request, User.UserId(), User.UserDisplayName(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }
}
