namespace SplitDuo.Api.Features.ExpenseAttachments.Dto;

public record AttachmentDownloadData(byte[] Content, string MimeType, string FilenameOriginal, string FileHash, long UpdatedAt);
