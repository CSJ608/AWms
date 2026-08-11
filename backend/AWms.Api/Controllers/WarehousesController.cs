using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Warehouses;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/warehouses")]
[RequirePermission("route.master-data")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly MasterDataService _service;

    public WarehousesController(MasterDataService service) => _service = service;

    [HttpPost("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<WarehouseItem>>>> Search([FromBody] FilterRequest request)
    {
        var result = await _service.SearchWarehousesAsync(request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WarehouseItem>>>> QuickSearch([FromQuery] string? keyword, [FromQuery] int pageSize = 10)
    {
        var result = await _service.SearchWarehousesAsync(new FilterRequest(keyword, null, null, null, null, null, null, null, null, null, 1, Math.Min(pageSize, 10)));
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var warehouse = await _service.GetWarehouseAsync(id);
        return warehouse != null ? Ok(ApiResponse.Ok(warehouse)) : NotFound(ApiResponse.Error<object>("WAREHOUSE_NOT_FOUND", "仓库不存在"));
    }

    [HttpPost]
    [RequirePermission("action.warehouse.create")]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request)
    {
        var result = await _service.CreateWarehouseAsync(request);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("action.warehouse.edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseRequest request)
    {
        var result = await _service.UpdateWarehouseAsync(id, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("action.warehouse.delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteWarehouseAsync(id);
        return NoContent();
    }

    // === Locations (nested) ===
    [HttpPost("{warehouseId:guid}/locations/search")]
    public async Task<ActionResult<ApiResponse<PagedResult<LocationItem>>>> SearchLocations(Guid warehouseId, [FromBody] FilterRequest request)
    {
        var result = await _service.SearchLocationsAsync(warehouseId, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{warehouseId:guid}/locations")]
    public async Task<ActionResult<ApiResponse<PagedResult<LocationItem>>>> QuickSearchLocations(Guid warehouseId, [FromQuery] string? keyword, [FromQuery] int pageSize = 10)
    {
        var result = await _service.SearchLocationsAsync(warehouseId, new FilterRequest(keyword, null, null, null, null, null, null, null, null, null, 1, Math.Min(pageSize, 10)));
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("{warehouseId:guid}/locations")]
    [RequirePermission("action.location.create")]
    public async Task<IActionResult> CreateLocation(Guid warehouseId, [FromBody] CreateLocationRequest request)
    {
        var result = await _service.CreateLocationAsync(warehouseId, request);
        return CreatedAtAction(nameof(SearchLocations), new { warehouseId }, ApiResponse.Ok(result));
    }

    [HttpPut("/api/locations/{id:guid}")]
    [RequirePermission("action.location.edit")]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationRequest request)
    {
        var result = await _service.UpdateLocationAsync(id, request);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("/api/locations/{id:guid}")]
    [RequirePermission("action.location.delete")]
    public async Task<IActionResult> DeleteLocation(Guid id)
    {
        await _service.DeleteLocationAsync(id);
        return NoContent();
    }
}
