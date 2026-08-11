using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AWms.Api.Middleware;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.ImportExport;
using AWms.Infrastructure.Services;

namespace AWms.Api.Controllers;

[ApiController]
[Route("api/import-export")]
[RequirePermission("route.master-data")]
[Authorize]
public class ImportExportController : ControllerBase
{
    private readonly ImportExportService _service;

    public ImportExportController(ImportExportService service) => _service = service;

    [HttpGet("templates/{moduleCode}")]
    public IActionResult DownloadTemplate(string moduleCode)
    {
        var data = _service.GenerateTemplate(moduleCode);
        return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{moduleCode}-template.xlsx");
    }

    [HttpPost("precheck")]
    [RequirePermission("action.import")]
    public async Task<ActionResult<ApiResponse<ImportTaskResponse>>> Precheck([FromForm] string moduleCode, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse.Error<object>("VALIDATION_ERROR", "文件为空"));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var fileBytes = ms.ToArray();

        var operatorIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid? operatorId = Guid.TryParse(operatorIdStr, out var id) ? id : null;

        var result = await _service.PrecheckAsync(moduleCode, fileBytes, file.FileName, operatorId);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("execute")]
    [RequirePermission("action.import")]
    public async Task<ActionResult<ApiResponse<ImportTaskResponse>>> Execute([FromBody] ExecuteRequest request)
    {
        var result = await _service.ExecuteAsync(request.TaskId);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("export")]
    [RequirePermission("action.export")]
    public async Task<IActionResult> CreateExport([FromBody] ExportRequest request)
    {
        var operatorIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid? operatorId = Guid.TryParse(operatorIdStr, out var id) ? id : null;
        var operatorName = User.FindFirst("name")?.Value;

        // filter/sort 透传（复验意见：禁止丢弃），pageSize=0 全量（契约）
        var filterReq = new FilterRequest(null, null, null, null, null, null, null, null, request.Sort, request.Filter, 1, request.PageSize ?? 0);
        var result = await _service.CreateExportAsync(request.ModuleCode, filterReq, operatorId, operatorName);
        return CreatedAtAction(nameof(GetTask), new { id = result.Id }, ApiResponse.Ok(result));
    }

    [HttpGet("tasks/{id:guid}")]
    public async Task<IActionResult> GetTask(Guid id)
    {
        var result = await _service.GetTaskAsync(id);
        return result != null ? Ok(ApiResponse.Ok(result)) : NotFound(ApiResponse.Error<object>("IMPORT_TASK_NOT_FOUND", "导入任务不存在"));
    }

    [HttpGet("tasks/{id:guid}/fail-report")]
    public async Task<IActionResult> DownloadFailReport(Guid id)
    {
        var (data, fileName) = await _service.GetFailReportAsync(id);
        return File(data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName ?? "fail-report.xlsx");
    }

    [HttpGet("tasks/{id:guid}/file")]
    public async Task<IActionResult> DownloadTaskFile(Guid id)
    {
        var (data, fileName) = await _service.GetTaskFileAsync(id);
        return File(data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName ?? "export.xlsx");
    }
}
