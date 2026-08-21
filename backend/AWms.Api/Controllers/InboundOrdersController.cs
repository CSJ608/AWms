using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Inbound;
using AWms.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AWms.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission("route.inbound")]
[Route("api/inbound-orders")]
public class InboundOrdersController : ControllerBase
{
    private readonly InboundOrderService _service;

    public InboundOrdersController(InboundOrderService service) => _service = service;

    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<InboundOrderItem>>>> Search([FromBody] InboundOrderSearchRequest request, CancellationToken ct)
    {
        var result = await _service.SearchAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<InboundOrderItem>>> Get(Guid id, CancellationToken ct)
    {
        var result = await _service.GetAsync(id, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost]
    [RequirePermission("action.inbound-order.create")]
    [RequireIdempotencyKey]
    public async Task<IActionResult> Create([FromBody] CreateInboundOrderRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, User.UserDisplayName(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpPost("{id:guid}/void")]
    [RequirePermission("action.inbound-order.void")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<InboundOrderItem>>> Void(Guid id, [FromBody] VoidInboundOrderRequest request, CancellationToken ct)
    {
        var result = await _service.VoidAsync(id, request.Reason, User.UserDisplayName(), ct);
        return Ok(ApiResponse.Ok(result));
    }
}
