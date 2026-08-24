using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.UserAvatars.Dto;
using SplitDuo.Api.Features.UserAvatars.Services;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.UserAvatars.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UserAvatarsController(
    IUserAvatarsService avatarsService,
    IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpPut("me/avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> UploadAvatar(IFormFile? file)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await avatarsService.UploadAvatarAsync(currentUserId.Value, file!);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Avatar uploaded");
    }

    [HttpDelete("me/avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAvatar()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await avatarsService.DeleteAvatarAsync(currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Avatar deleted");
    }

    [HttpGet("{userId}/avatar")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAvatar(string userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await avatarsService.DownloadAvatarAsync(userId, currentUserId.Value);

        if (!result.IsSuccess)
            return HandleResult(result.ToResult(), "Download failed");

        var data = result.Value!;
        var etag = $"\"{data.FileHash}\"";
        // Conditional GET: return 304 if the client's ETag matches
        if (Request.Headers.IfNoneMatch == etag)
        {
            Response.Headers.ETag = etag;
            return StatusCode(304);
        }
        var lastModified = DateTimeOffset.UnixEpoch.AddSeconds(data.UpdatedAt);
        Response.Headers.ETag = etag;
        Response.Headers["Last-Modified"] = lastModified.ToString("R");
        Response.Headers.CacheControl = "private";
        Response.Headers.ContentDisposition = $"inline; filename=\"{data.FilenameOriginal}\"";

        return File(data.Content, data.MimeType);
    }
}
