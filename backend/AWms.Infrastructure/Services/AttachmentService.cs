using System.Collections.Concurrent;
using AWms.Domain.Dtos.Attachments;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Entities;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AWms.Infrastructure.Services;

public class AttachmentService
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ThumbnailLocks = new();

    private readonly AWmsDbContext _db;
    private readonly string _root;

    public AttachmentService(AWmsDbContext db, IConfiguration configuration)
    {
        _db = db;
        _root = configuration["Storage:AttachmentsRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "attachments");
    }

    public async Task<AttachmentItem> UploadAsync(
        string fileName,
        string mimeType,
        long size,
        Stream content,
        Guid uploadedBy,
        string uploadedByName,
        CancellationToken ct = default)
    {
        if (size <= 0)
            throw new DomainException("VALIDATION_ERROR", "file 必填", 400);
        if (size > 10 * 1024 * 1024)
            throw new DomainException("ATTACHMENT_TOO_LARGE", "附件不能超过 10MB", 413);
        if (!AllowedMimeTypes.Contains(mimeType))
            throw new DomainException("ATTACHMENT_TYPE_INVALID", "仅支持 jpg/png/webp", 400);

        var extension = mimeType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
        var bucket = Path.Combine("UNASSIGNED", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var dir = Path.Combine(_root, bucket);
        Directory.CreateDirectory(dir);

        var storedName = $"{Guid.CreateVersion7():N}{extension}";
        var physicalPath = Path.Combine(dir, storedName);
        await using (var file = File.Create(physicalPath))
        {
            await content.CopyToAsync(file, ct);
        }

        var attachment = new Attachment
        {
            FileName = Path.GetFileName(fileName),
            MimeType = mimeType,
            Size = size,
            Path = physicalPath,
            UploadedBy = uploadedBy,
            UploadedByName = uploadedByName,
            UploadedAt = DateTime.UtcNow
        };
        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync(ct);
        return Map(attachment);
    }

    public async Task<PagedResult<AttachmentItem>> SearchAsync(
        AttachmentSearchRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var page = Math.Max(request.Page ?? 1, 1);
        var pageSize = request.PageSize is null or <= 0 ? 20 : request.PageSize.Value;
        var query = _db.Attachments.AsNoTracking()
            .Where(x => x.BizId != null || x.UploadedBy == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.BizType))
            query = query.Where(x => x.BizType == request.BizType);
        if (request.BizId.HasValue)
            query = query.Where(x => x.BizId == request.BizId.Value);
        if (request.UploadedBy.HasValue)
            query = query.Where(x => x.UploadedBy == request.UploadedBy.Value);
        if (request.DateFrom.HasValue)
            query = query.Where(x => x.UploadedAt >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(x => x.UploadedAt < request.DateTo.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.UploadedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<AttachmentItem>(items.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<Attachment> GetEntityAsync(Guid id, CancellationToken ct = default) =>
        await _db.Attachments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("ATTACHMENT_NOT_FOUND", "附件不存在", 404);

    public async Task<(string Path, string MimeType, string FileName)> GetFileAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var attachment = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("ATTACHMENT_NOT_FOUND", "附件不存在", 404);
        if (attachment.BizId == null && attachment.UploadedBy != userId)
            throw new DomainException("FORBIDDEN", "无权查看该附件", 403);
        if (!File.Exists(attachment.Path))
            throw new DomainException("ATTACHMENT_NOT_FOUND", "附件文件不存在", 404);
        return (attachment.Path, attachment.MimeType, attachment.FileName);
    }

    public async Task<(string Path, string MimeType, string FileName)> GetThumbnailAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var source = await GetFileAsync(id, userId, ct);
        var directory = Path.Combine(Path.GetDirectoryName(source.Path)!, ".thumbnails");
        var thumbnailPath = Path.Combine(directory, $"{id:N}.jpg");
        if (File.Exists(thumbnailPath))
            return (thumbnailPath, "image/jpeg", $"{Path.GetFileNameWithoutExtension(source.FileName)}-thumbnail.jpg");

        var semaphore = ThumbnailLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            if (!File.Exists(thumbnailPath))
            {
                Directory.CreateDirectory(directory);
                var tempPath = Path.Combine(directory, $".{id:N}.{Guid.CreateVersion7():N}.tmp");
                try
                {
                    using var image = await Image.LoadAsync(source.Path, ct);
                    image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(320, 320)
                    }));
                    await image.SaveAsJpegAsync(tempPath, new JpegEncoder { Quality = 78 }, ct);
                    File.Move(tempPath, thumbnailPath, overwrite: true);
                }
                catch (InvalidImageContentException)
                {
                    throw new DomainException("ATTACHMENT_TYPE_INVALID", "附件内容不是有效图片", 400);
                }
                catch (UnknownImageFormatException)
                {
                    throw new DomainException("ATTACHMENT_TYPE_INVALID", "附件内容不是有效图片", 400);
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            }
        }
        finally
        {
            semaphore.Release();
        }

        return (thumbnailPath, "image/jpeg", $"{Path.GetFileNameWithoutExtension(source.FileName)}-thumbnail.jpg");
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var attachment = await GetEntityAsync(id, ct);
        if (attachment.BizId.HasValue || !string.IsNullOrWhiteSpace(attachment.BizType))
            throw new DomainException("ATTACHMENT_IN_USE", "已关联业务的附件禁止删除", 409);
        if (attachment.UploadedBy != userId)
            throw new DomainException("FORBIDDEN", "只能删除自己上传的未关联附件", 403);

        _db.Attachments.Remove(attachment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClaimAsync(
        IEnumerable<Guid> ids,
        string bizType,
        Guid bizId,
        Guid userId,
        CancellationToken ct = default)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
            return;

        var updated = await _db.Attachments
            .Where(x => distinctIds.Contains(x.Id) && x.UploadedBy == userId && x.BizType == null && x.BizId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.BizType, bizType)
                .SetProperty(x => x.BizId, bizId), ct);

        if (updated != distinctIds.Count)
            throw new DomainException("ATTACHMENT_IN_USE", "附件已被关联或不可用", 409);
    }

    public static AttachmentItem Map(Attachment attachment) =>
        new(
            attachment.Id,
            attachment.FileName,
            attachment.MimeType,
            attachment.Size,
            attachment.BizType,
            attachment.BizId,
            attachment.UploadedBy,
            attachment.UploadedByName,
            attachment.UploadedAt,
            $"/api/attachments/{attachment.Id}",
            $"/api/attachments/{attachment.Id}/thumbnail");
}
