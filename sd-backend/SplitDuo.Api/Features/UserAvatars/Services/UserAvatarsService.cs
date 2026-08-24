using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.UserAvatars.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.UserAvatars.Services;

public class UserAvatarsService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IStringLocalizer<UserAvatarsService> loc) : IUserAvatarsService
{
    private const long MaxAvatarSizeBytes = 200 * 1024; // 200KB
    private const int MaxFilenameLength = 255;

    private static readonly HashSet<string> AllowedAvatarMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public async Task<Result> UploadAvatarAsync(Guid currentUserId, IFormFile file)
    {
        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized(loc["UserNotFound"]);

        if (file is null || file.Length == 0)
            return Result.BadRequest(loc["NoFileUploaded"]);

        if (file.Length > MaxAvatarSizeBytes)
            return Result.BadRequest(loc["FileSizeExceeded"]);

        if (!FileValidation.IsValidExtension(file.FileName))
            return Result.BadRequest(loc["FileTypeNotAllowed"]);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var detected = FileValidation.SniffMagicNumber(bytes);
        if (detected is null)
            return Result.BadRequest(loc["FileContentMismatch"]);

        // Post-sniff MIME filter: only raster image formats are accepted for avatars
        if (!AllowedAvatarMimeTypes.Contains(detected))
            return Result.BadRequest(loc["FileTypeNotAllowed"]);

        var fileHash = HashUtils.ComputeSha256(bytes);

        var ext = FileValidation.NormalizeExtension(file.FileName);
        var storedFilename = $"{fileHash}{ext}";

        // Sanitize original filename: strip path components and truncate to column limit
        var sanitizedFilename = Path.GetFileName(file.FileName);
        if (sanitizedFilename.Length > MaxFilenameLength)
            sanitizedFilename = sanitizedFilename[..MaxFilenameLength];

        // Replace-on-upload: remove any existing avatar, then insert the new one.
        // Both changes are tracked and committed together by the controller's SaveChangesAsync.
        var existing = await unitOfWork.UserAvatars
            .FirstOrDefaultAsync(a => a.UserId == currentUser.Id);

        if (existing != null)
            unitOfWork.UserAvatars.Remove(existing);

        var avatar = new UserAvatar
        {
            UserId = currentUser.Id,
            FilenameOriginal = sanitizedFilename,
            StoredFilename = storedFilename,
            FileHash = fileHash,
            MimeType = detected, // from magic number sniff, NOT file.ContentType
            SizeBytes = bytes.Length,
            Content = bytes
        };

        unitOfWork.UserAvatars.Add(avatar);

        return Result.Success(HttpStatusCode.NoContent);
    }

    public async Task<Result> DeleteAvatarAsync(Guid currentUserId)
    {
        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized(loc["UserNotFound"]);

        var avatar = await unitOfWork.UserAvatars
            .FirstOrDefaultAsync(a => a.UserId == currentUser.Id);

        if (avatar == null)
            return Result.NotFound(loc["AvatarNotFound"]);

        unitOfWork.UserAvatars.Remove(avatar);

        return Result.Success(HttpStatusCode.NoContent);
    }

    public async Task<Result<AvatarDownloadData>> DownloadAvatarAsync(string userId, Guid currentUserId)
    {
        if (!Guid.TryParse(userId, out var targetGuid))
            return Result<AvatarDownloadData>.BadRequest(loc["InvalidUserIdFormat"]);

        // Any authenticated user can fetch any user's avatar (no group membership check)
        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<AvatarDownloadData>.Unauthorized(loc["UserNotFound"]);

        var targetUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == targetGuid);

        if (targetUser == null)
            return Result<AvatarDownloadData>.NotFound(loc["UserNotFound"]);

        var avatar = await unitOfWork.UserAvatars
            .FirstOrDefaultAsync(a => a.UserId == targetUser.Id);

        if (avatar == null)
            return Result<AvatarDownloadData>.NotFound(loc["AvatarNotFound"]);

        var data = new AvatarDownloadData(
            avatar.Content, avatar.MimeType, avatar.FilenameOriginal, avatar.FileHash, avatar.UpdatedAt);

        return Result<AvatarDownloadData>.Success(data);
    }
}
