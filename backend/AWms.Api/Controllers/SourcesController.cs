using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Sources;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/sources")]
[RequirePermission("route.master-data")]
[Authorize]
public class SourcesController : ControllerBase
{
    private readonly MasterDataService _service;

    public SourcesController(MasterDataService service) => _service = service;

    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<SourceItem>>>> Search([FromBody] FilterRequest request)
    {
        var result = await _service.SearchSourcesAsync(request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SourceItem>>>> QuickSearch([FromQuery] string? keyword, [FromQuery] int pageSize = 10)
    {
        var result = await _service.SearchSourcesAsync(new FilterRequest(keyword, null, null, null, null, null, null, null, null, null, 1, Math.Min(pageSize, 10)));
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var source = await _service.GetSourceAsync(id);
        return source != null ? Ok(ApiResponse.Ok(source)) : NotFound(ApiResponse.Error<object>("SOURCE_NOT_FOUND", "来源不存在"));
    }

    [HttpPost]
    [RequirePermission("action.source.create")]
    public async Task<IActionResult> Create([FromBody] CreateSourceRequest request)
    {
        var result = await _service.CreateSourceAsync(request);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("action.source.edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSourceRequest request)
    {
        var result = await _service.UpdateSourceAsync(id, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("action.source.delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteSourceAsync(id);
        return NoContent();
    }
}
