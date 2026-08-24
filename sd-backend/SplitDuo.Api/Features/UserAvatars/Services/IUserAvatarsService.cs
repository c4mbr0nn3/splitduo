using SplitDuo.Api.Features.UserAvatars.Dto;
using SplitDuo.Core.Common;

namespace SplitDuo.Api.Features.UserAvatars.Services;

public interface IUserAvatarsService
{
    Task<Result> UploadAvatarAsync(Guid currentUserId, IFormFile file);
    Task<Result> DeleteAvatarAsync(Guid currentUserId);
    Task<Result<AvatarDownloadData>> DownloadAvatarAsync(string userId, Guid currentUserId);
}
