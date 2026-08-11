using Microsoft.AspNetCore.Authorization;
using AWms.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using AWms.Domain.Dtos.Batches;
using AWms.Domain.Dtos.Common;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/batches")]
[RequirePermission("route.master-data")]
[Authorize]
public class BatchesController : ControllerBase
{
    private readonly MasterDataService _service;

    public BatchesController(MasterDataService service) => _service = service;

    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<BatchItem>>>> Search([FromBody] FilterRequest request)
    {
        var result = await _service.SearchBatchesAsync(request);
        return Ok(ApiResponse.Ok(result));
    }

    // 契约：POST /api/materials/{materialId}/batches/search（某物料批次列表）
    [HttpPost("/api/materials/{materialId:guid}/batches/search")]
    public async Task<ActionResult<ApiResponse<PagedResult<BatchItem>>>> SearchMaterialBatches(Guid materialId, [FromBody] FilterRequest request)
    {
        var result = await _service.SearchMaterialBatchesAsync(materialId, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<BatchItem>>>> QuickSearch([FromQuery] string? keyword, [FromQuery] int pageSize = 10)
    {
        var result = await _service.SearchBatchesAsync(new FilterRequest(keyword, null, null, null, null, null, null, null, null, null, 1, Math.Min(pageSize, 10)));
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _service.GetBatchAsync(id);
        return result != null ? Ok(ApiResponse.Ok(result)) : NotFound(ApiResponse.Error<object>("BATCH_NOT_FOUND", "批次不存在"));
    }
}
