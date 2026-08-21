using AWms.Api.Middleware;
using AWms.Domain.Dtos.Attachments;
using AWms.Domain.Dtos.Common;
using AWms.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AWms.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission("route.inbound")]
[Route("api/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly AttachmentService _service;

    public AttachmentsController(AttachmentService service) => _service = service;

    [HttpPost]
    [RequirePermission("action.attachment.upload")]
    [RequireIdempotencyKey]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string? bizType, CancellationToken ct)
    {
        EnsureBusinessUploadPermission(bizType);
        var result = await _service.UploadAsync(file.FileName, file.ContentType, file.Length, file.OpenReadStream(), User.UserId(), User.UserDisplayName(), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var file = await _service.GetFileAsync(id, User.UserId(), ct);
        return PhysicalFile(file.Path, file.MimeType, file.FileName, enableRangeProcessing: false);
    }

    [HttpGet("{id:guid}/thumbnail")]
    public async Task<IActionResult> Thumbnail(Guid id, CancellationToken ct)
    {
        var file = await _service.GetThumbnailAsync(id, User.UserId(), ct);
        return PhysicalFile(file.Path, file.MimeType, file.FileName, enableRangeProcessing: false);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AttachmentItem>>>> Search(
        [FromQuery] string? bizType,
        [FromQuery] Guid? bizId,
        [FromQuery] Guid? uploadedBy,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var result = await _service.SearchAsync(
            new AttachmentSearchRequest(bizType, bizId, uploadedBy, dateFrom, dateTo, page, pageSize),
            User.UserId(),
            ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("action.attachment.upload")]
    [RequireIdempotencyKey]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, User.UserId(), ct);
        return NoContent();
    }

    private void EnsureBusinessUploadPermission(string? bizType)
    {
        var normalized = bizType?.Trim().ToUpperInvariant();
        var allowed = normalized switch
        {
            "RECEIPT" => User.HasClaim("permission", "action.receiving.create"),
            "EXCEPTION" or "QUALITY_CHECK" => User.HasClaim("permission", "action.quality.check"),
            null or "" => User.HasClaim("permission", "action.receiving.create") ||
                          User.HasClaim("permission", "action.quality.check"),
            _ => throw new DomainException("VALIDATION_ERROR", "bizType 值无效", 400)
        };
        if (!allowed)
            throw new DomainException("FORBIDDEN", "缺少对应业务操作权限", 403);
    }
}
