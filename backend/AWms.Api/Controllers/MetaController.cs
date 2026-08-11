using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/meta")]
[Authorize]
[RequirePermission("route.master-data")]
public class MetaController : ControllerBase
{
    [HttpGet("fields/{resource}")]
    public ActionResult<ApiResponse<List<FieldMeta>>> GetFields(string resource)
    {
        var fields = resource.ToLowerInvariant() switch
        {
            "materials" => new List<FieldMeta>
            {
                new("code", "field.code", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("name", "field.name", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("searchCode", "field.searchCode", "string", new() { "eq", "contains", "in" }),
                new("batchControlled", "field.batchControlled", "bool", new() { "eq" }),
                new("labelType", "field.labelType", "enum", new() { "eq", "in" }, Options: new() { new("NONE", "enum.labelType.none"), new("SKU", "enum.labelType.sku"), new("UNIQUE", "enum.labelType.unique") }),
                new("defaultUom", "field.defaultUom", "enum", new() { "eq", "in" }, Options: new() { new("CT", "unit.ct"), new("PC", "unit.pc"), new("BOX", "unit.box"), new("KG", "unit.kg") }),
                new("defaultQtyPerLabel", "field.defaultQtyPerLabel", "decimal", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
                new("status", "field.status", "enum", new() { "eq", "in" }, Options: new() { new("ENABLED", "status.enabled"), new("DISABLED", "status.disabled") }),
                new("createdAt", "field.createdAt", "datetime", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
                new("updatedAt", "field.updatedAt", "datetime", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
            },
            "warehouses" => new List<FieldMeta>
            {
                new("code", "field.code", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("name", "field.name", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("searchCode", "field.searchCode", "string", new() { "eq", "contains", "in" }),
                new("status", "field.status", "enum", new() { "eq", "in" }, Options: new() { new("ENABLED", "status.enabled"), new("DISABLED", "status.disabled") }),
                new("mgmtMode", "field.mgmtMode", "enum", new() { "eq", "in" }, Options: new() { new("MANUAL", "mode.manual"), new("AGV", "mode.agv") }),
                new("createdAt", "field.createdAt", "datetime", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
            },
            "locations" => new List<FieldMeta>
            {
                new("code", "field.code", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("searchCode", "field.searchCode", "string", new() { "eq", "contains", "in" }),
                new("type", "field.type", "enum", new() { "eq", "in" }, Options: new() { new("STAGING", "type.staging"), new("DEFAULT", "type.default") }),
                new("status", "field.status", "enum", new() { "eq", "in" }, Options: new() { new("ENABLED", "status.enabled"), new("DISABLED", "status.disabled") }),
                new("reachability", "field.reachability", "enum", new() { "eq", "in" }, Options: new() { new("MANUAL_ONLY", "reach.manual"), new("AGV", "reach.agv"), new("UNIVERSAL", "reach.universal") }),
                new("createdAt", "field.createdAt", "datetime", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
            },
            "sources" => new List<FieldMeta>
            {
                new("type", "field.type", "enum", new() { "eq", "in" }, Options: new() { new("SUPPLIER", "type.supplier"), new("WORKSHOP", "type.workshop") }),
                new("code", "field.code", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("name", "field.name", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("searchCode", "field.searchCode", "string", new() { "eq", "contains", "in" }),
                new("status", "field.status", "enum", new() { "eq", "in" }, Options: new() { new("ENABLED", "status.enabled"), new("DISABLED", "status.disabled") }),
                new("createdAt", "field.createdAt", "datetime", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
            },
            "batches" => new List<FieldMeta>
            {
                new("materialId", "field.materialId", "uuid", new() { "eq", "in" }),
                new("materialCode", "field.materialCode", "string", new() { "eq", "contains", "in" }),
                new("batchNo", "field.batchNo", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("sourceBatchNo", "field.sourceBatchNo", "string", new() { "eq", "contains", "in" }),
                new("sourceType", "field.sourceType", "enum", new() { "eq", "in" }, Options: new() { new("SUPPLIER", "type.supplier"), new("WORKSHOP", "type.workshop") }),
                new("productionDate", "field.productionDate", "date", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
                new("expiryDate", "field.expiryDate", "date", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
                new("status", "field.status", "enum", new() { "eq", "in" }, Options: new() { new("ACTIVE", "status.active"), new("CLOSED", "status.closed") }),
                new("createdAt", "field.createdAt", "datetime", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
            },
            "users" => new List<FieldMeta>
            {
                new("username", "field.username", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("name", "field.name", "string", new() { "eq", "contains", "startsWith", "in" }),
                new("status", "field.status", "enum", new() { "eq", "in" }, Options: new() { new("ACTIVE", "status.active"), new("DISABLED", "status.disabled") }),
                new("createdAt", "field.createdAt", "datetime", new() { "eq", "gt", "gte", "lt", "lte", "between" }),
            },
            _ => new List<FieldMeta>()
        };

        if (fields.Count == 0)
            return NotFound(ApiResponse.Error<object>("NOT_FOUND", $"Resource '{resource}' not found"));

        return Ok(ApiResponse.Ok(fields));
    }
}
