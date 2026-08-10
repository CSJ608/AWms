using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Materials;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/materials")]
[Authorize]
public class MaterialsController : ControllerBase
{
    private readonly MasterDataService _service;

    public MaterialsController(MasterDataService service) => _service = service;

    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<MaterialItem>>>> Search([FromBody] FilterRequest request)
    {
        var result = await _service.SearchMaterialsAsync(request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<MaterialItem>>>> QuickSearch([FromQuery] string? keyword, [FromQuery] int pageSize = 10)
    {
        var result = await _service.SearchMaterialsAsync(new FilterRequest(keyword, null, null, null, null, null, null, null, null, null, 1, Math.Min(pageSize, 10)));
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _service.GetMaterialAsync(id);
        return result != null ? Ok(ApiResponse.Ok(result)) : NotFound(ApiResponse.Error<object>("MATERIAL_NOT_FOUND", "物料不存在"));
    }

    [HttpPost]
    [RequirePermission("action.material.create")]
    public async Task<IActionResult> Create([FromBody] CreateMaterialRequest request)
    {
        var result = await _service.CreateMaterialAsync(request);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("action.material.edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMaterialRequest request)
    {
        var result = await _service.UpdateMaterialAsync(id, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("action.material.delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteMaterialAsync(id);
        return NoContent();
    }
}
