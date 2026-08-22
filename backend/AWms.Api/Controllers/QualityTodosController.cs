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
[Route("api/quality-todos")]
public class QualityTodosController : ControllerBase
{
    private readonly ReceiptService _service;

    public QualityTodosController(ReceiptService service) => _service = service;

    [HttpPost("search")]
    [RequirePermission("action.quality.check")]
    public async Task<ActionResult<ApiResponse<PagedResult<QualityTodoItem>>>> Search([FromBody] QualityTodoSearchRequest request, CancellationToken ct)
    {
        var result = await _service.SearchQualityTodosAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }
}
