using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Scan;
using AWms.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AWms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/scan")]
public class ScanController : ControllerBase
{
    private readonly ScanService _service;

    public ScanController(ScanService service) => _service = service;

    [HttpPost("parse")]
    public async Task<ActionResult<ApiResponse<ScanResult>>> Parse([FromBody] ScanParseRequest request, CancellationToken ct)
    {
        var result = await _service.ParseAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }
}
