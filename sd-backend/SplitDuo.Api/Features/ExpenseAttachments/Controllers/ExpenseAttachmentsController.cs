using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.ExpenseAttachments.Dto;
using SplitDuo.Api.Features.ExpenseAttachments.Services;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.ExpenseAttachments.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/expenses/{expenseId}/attachments")]
[Authorize]
public class ExpenseAttachmentsController(
    IExpenseAttachmentsService attachmentsService,
    IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponseDto<ExpenseAttachmentDto>>> UploadAttachment(
        string groupId, string expenseId, IFormFile? file)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<ExpenseAttachmentDto>());

        var result = await attachmentsService.UploadAttachmentAsync(groupId, expenseId, currentUserId.Value, file!);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Attachment uploaded successfully");
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<ExpenseAttachmentDto>>>> ListAttachments(
        string groupId, string expenseId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<List<ExpenseAttachmentDto>>());

        var result = await attachmentsService.ListAttachmentsAsync(groupId, expenseId, currentUserId.Value);
        return HandleResult(result, "Attachments retrieved successfully");
    }

    [HttpGet("{attachmentId}")]
    public async Task<IActionResult> DownloadAttachment(string groupId, string expenseId, string attachmentId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await attachmentsService.DownloadAttachmentAsync(groupId, expenseId, attachmentId, currentUserId.Value);

        if (!result.IsSuccess)
            return HandleResult(result.ToResult(), "Download failed");

        var data = result.Value!;
        var etag = $"\"{data.FileHash}\"";
        var lastModified = DateTimeOffset.UnixEpoch.AddSeconds(data.UpdatedAt);
        Response.Headers.ETag = etag;
        Response.Headers["Last-Modified"] = lastModified.ToString("R");
        Response.Headers.CacheControl = "private";
        Response.Headers.ContentDisposition = $"inline; filename=\"{data.FilenameOriginal}\"";

        return File(data.Content, data.MimeType);
    }

    [HttpDelete("{attachmentId}")]
    public async Task<ActionResult> DeleteAttachment(string groupId, string expenseId, string attachmentId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await attachmentsService.DeleteAttachmentAsync(groupId, expenseId, attachmentId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Attachment deleted successfully");
    }
}
