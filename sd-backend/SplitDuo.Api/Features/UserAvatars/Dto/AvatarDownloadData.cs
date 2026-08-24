namespace SplitDuo.Api.Features.UserAvatars.Dto;

public record AvatarDownloadData(byte[] Content, string MimeType, string FilenameOriginal, string FileHash, long UpdatedAt);
