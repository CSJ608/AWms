namespace AWms.Domain.Dtos.Attachments;

public record AttachmentItem(
    Guid Id,
    string FileName,
    string MimeType,
    long Size,
    string? BizType,
    Guid? BizId,
    Guid UploadedBy,
    string UploadedByName,
    DateTime UploadedAt,
    string Url,
    string ThumbnailUrl);

public record AttachmentSearchRequest(
    string? BizType,
    Guid? BizId,
    Guid? UploadedBy,
    DateTime? DateFrom,
    DateTime? DateTo,
    int? Page,
    int? PageSize);
