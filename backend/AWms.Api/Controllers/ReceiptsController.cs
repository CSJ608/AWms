using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Print;
using AWms.Domain.Dtos.Receipts;
using AWms.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AWms.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission("route.inbound")]
[Route("api/receipts")]
public class ReceiptsController : ControllerBase
{
    private readonly ReceiptService _receipts;
    private readonly PrintService _print;

    public ReceiptsController(ReceiptService receipts, PrintService print)
    {
        _receipts = receipts;
        _print = print;
    }

    [HttpPost]
    [RequirePermission("action.receiving.create")]
    [RequireIdempotencyKey]
    public async Task<IActionResult> Submit([FromBody] SubmitReceiptRequest request, CancellationToken ct)
    {
        var result = await _receipts.SubmitAsync(request, User.UserId(), User.UserDisplayName(), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<ReceiptItem>>>> Search([FromBody] ReceiptSearchRequest request, CancellationToken ct)
    {
        var result = await _receipts.SearchAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReceiptItem>>> Get(Guid id, CancellationToken ct)
    {
        var result = await _receipts.GetAsync(id, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("{id:guid}/print")]
    [RequirePermission("action.print.create")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<PrintJobDto>>> Print(Guid id, CancellationToken ct)
    {
        var result = await _print.PrintReceiptAsync(id, User.UserId(), User.UserDisplayName(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }
}
