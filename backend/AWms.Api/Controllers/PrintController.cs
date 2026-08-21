using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Print;
using AWms.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AWms.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission("route.inbound")]
[Route("api/print")]
public class PrintController : ControllerBase
{
    private readonly PrintService _service;

    public PrintController(PrintService service) => _service = service;

    [HttpPost("inbound-order-qr")]
    [RequirePermission("action.print.create")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<PrintJobDto>>> InboundOrderQr([FromBody] InboundOrderQrPrintRequest request, CancellationToken ct)
    {
        var result = await _service.PrintInboundOrderQrAsync(request, User.UserId(), User.UserDisplayName(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPost("external-labels")]
    [RequirePermission("action.print.create")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<PrintJobDto>>> ExternalLabels([FromBody] ExternalLabelPrintRequest request, CancellationToken ct)
    {
        var result = await _service.PrintExternalLabelsAsync(request, User.UserId(), User.UserDisplayName(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPost("unique-labels")]
    [RequirePermission("action.print.create")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<PrintJobDto>>> UniqueLabels([FromBody] UniqueLabelsPrintRequest request, CancellationToken ct)
    {
        var result = await _service.PrintUniqueLabelsAsync(request, User.UserId(), User.UserDisplayName(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPost("batch-labels")]
    [RequirePermission("action.print.create")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<PrintJobDto>>> BatchLabels([FromBody] BatchLabelsPrintRequest request, CancellationToken ct)
    {
        var result = await _service.PrintBatchLabelsAsync(request, User.UserId(), User.UserDisplayName(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPost("batch-label-one")]
    [RequirePermission("action.print.create")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<PrintJobDto>>> BatchLabelOne([FromBody] BatchLabelOnePrintRequest request, CancellationToken ct)
    {
        var result = await _service.PrintBatchLabelOneAsync(request, User.UserId(), User.UserDisplayName(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPost("jobs/search")]
    public async Task<ActionResult<ApiResponse<PagedResult<PrintJobDto>>>> SearchJobs([FromBody] PrintJobSearchRequest request, CancellationToken ct)
    {
        var result = await _service.SearchJobsAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PrintJobDto>>> GetJob(Guid id, CancellationToken ct)
    {
        var result = await _service.GetJobAsync(id, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("jobs/{id:guid}/retry")]
    [RequirePermission("action.print.create")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<ApiResponse<PrintJobDto>>> Retry(Guid id, CancellationToken ct)
    {
        var result = await _service.RetryAsync(id, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("jobs/{id:guid}/file")]
    public async Task<IActionResult> File(Guid id, CancellationToken ct)
    {
        var file = await _service.GetFileAsync(id, ct);
        return PhysicalFile(file.Path, "application/pdf", file.FileName, enableRangeProcessing: false);
    }
}
