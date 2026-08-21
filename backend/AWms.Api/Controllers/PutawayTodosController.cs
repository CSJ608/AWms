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
[Route("api/putaway-todos")]
public class PutawayTodosController : ControllerBase
{
    private readonly ReceiptService _service;

    public PutawayTodosController(ReceiptService service) => _service = service;

    [HttpPost("search")]
    [RequirePermission("action.putaway.create")]
    public async Task<ActionResult<ApiResponse<PagedResult<PutawayTodoItem>>>> Search([FromBody] PutawayTodoSearchRequest request, CancellationToken ct)
    {
        var result = await _service.SearchPutawayTodosAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{receiptLineId:guid}/recommendations")]
    [RequirePermission("action.putaway.create")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LocationRecommendationItem>>>> Recommendations(Guid receiptLineId, CancellationToken ct)
    {
        var result = await _service.GetRecommendationsAsync(receiptLineId, ct);
        return Ok(ApiResponse.Ok(result));
    }
}
