using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.ExpenseAttachments.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.ExpenseAttachments.Services;

public interface IExpenseAttachmentsService
{
    Task<Result<ExpenseAttachmentDto>> UploadAttachmentAsync(string groupId, string expenseId, Guid currentUserId, IFormFile file);
    Task<Result<List<ExpenseAttachmentDto>>> ListAttachmentsAsync(string groupId, string expenseId, Guid currentUserId);
    Task<Result<AttachmentDownloadData>> DownloadAttachmentAsync(string groupId, string expenseId, string attachmentId, Guid currentUserId);
    Task<Result> DeleteAttachmentAsync(string groupId, string expenseId, string attachmentId, Guid currentUserId);
}

public class ExpenseAttachmentsService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IStringLocalizer<ExpenseAttachmentsService> loc) : IExpenseAttachmentsService
{
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024; // 10MB
    private const int MaxAttachmentsPerExpense = 5;
    private const int MaxFilenameLength = 255;

    public async Task<Result<ExpenseAttachmentDto>> UploadAttachmentAsync(string groupId, string expenseId, Guid currentUserId, IFormFile file)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<ExpenseAttachmentDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result<ExpenseAttachmentDto>.BadRequest(loc["InvalidExpenseIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<ExpenseAttachmentDto>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<ExpenseAttachmentDto>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<ExpenseAttachmentDto>.Forbidden(loc["AccessNotAllowed"]);

        var expense = await unitOfWork.Expenses
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result<ExpenseAttachmentDto>.NotFound(loc["ExpenseNotFound"]);

        if (file is null || file.Length == 0)
            return Result<ExpenseAttachmentDto>.BadRequest(loc["NoFileUploaded"]);

        if (file.Length > MaxAttachmentSizeBytes)
            return Result<ExpenseAttachmentDto>.BadRequest(loc["FileSizeExceeded"]);

        if (!FileValidation.IsValidExtension(file.FileName))
            return Result<ExpenseAttachmentDto>.BadRequest(loc["FileTypeNotAllowed"]);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var detected = FileValidation.SniffMagicNumber(bytes);
        if (detected is null)
            return Result<ExpenseAttachmentDto>.BadRequest(loc["FileContentMismatch"]);

        var fileHash = HashUtils.ComputeSha256(bytes);

        // Dedup check: same file already attached to this expense
        var existing = await unitOfWork.ExpenseAttachments
            .AnyAsync(a => a.ExpenseId == expense.Id && a.FileHash == fileHash);

        if (existing)
            return Result<ExpenseAttachmentDto>.BadRequest(loc["DuplicateAttachment"]);

        // Cap check: max 5 attachments per expense
        var count = await unitOfWork.ExpenseAttachments
            .CountAsync(a => a.ExpenseId == expense.Id);

        if (count >= MaxAttachmentsPerExpense)
            return Result<ExpenseAttachmentDto>.BadRequest(loc["AttachmentLimitReached"]);

        var ext = FileValidation.NormalizeExtension(file.FileName);
        var storedFilename = $"{fileHash}{ext}";

        // Sanitize original filename: strip path components and truncate to column limit
        var sanitizedFilename = Path.GetFileName(file.FileName);
        if (sanitizedFilename.Length > MaxFilenameLength)
            sanitizedFilename = sanitizedFilename[..MaxFilenameLength];

        var attachment = new ExpenseAttachment
        {
            ExpenseId = expense.Id,
            FilenameOriginal = sanitizedFilename,
            StoredFilename = storedFilename,
            FileHash = fileHash,
            MimeType = detected, // from magic number sniff, NOT file.ContentType
            SizeBytes = bytes.Length,
            Content = bytes
        };

        unitOfWork.ExpenseAttachments.Add(attachment);

        var dto = new ExpenseAttachmentDto(attachment, expense.Guid.ToString());
        return Result<ExpenseAttachmentDto>.Success(dto, HttpStatusCode.Created);
    }

    public async Task<Result<List<ExpenseAttachmentDto>>> ListAttachmentsAsync(string groupId, string expenseId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<List<ExpenseAttachmentDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result<List<ExpenseAttachmentDto>>.BadRequest(loc["InvalidExpenseIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<List<ExpenseAttachmentDto>>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<List<ExpenseAttachmentDto>>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<List<ExpenseAttachmentDto>>.Forbidden(loc["AccessNotAllowed"]);

        var expense = await unitOfWork.Expenses
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result<List<ExpenseAttachmentDto>>.NotFound(loc["ExpenseNotFound"]);

        var attachments = await unitOfWork.ExpenseAttachments
            .Where(a => a.ExpenseId == expense.Id)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var dtoList = attachments
            .Select(a => new ExpenseAttachmentDto(a, expense.Guid.ToString()))
            .ToList();

        return Result<List<ExpenseAttachmentDto>>.Success(dtoList);
    }

    public async Task<Result<AttachmentDownloadData>> DownloadAttachmentAsync(string groupId, string expenseId, string attachmentId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<AttachmentDownloadData>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result<AttachmentDownloadData>.BadRequest(loc["InvalidExpenseIdFormat"]);

        if (!Guid.TryParse(attachmentId, out var attachmentGuid))
            return Result<AttachmentDownloadData>.BadRequest(loc["InvalidAttachmentIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<AttachmentDownloadData>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<AttachmentDownloadData>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<AttachmentDownloadData>.Forbidden(loc["AccessNotAllowed"]);

        var expense = await unitOfWork.Expenses
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result<AttachmentDownloadData>.NotFound(loc["ExpenseNotFound"]);

        var attachment = await unitOfWork.ExpenseAttachments
            .FirstOrDefaultAsync(a => a.Guid == attachmentGuid && a.ExpenseId == expense.Id);

        if (attachment == null)
            return Result<AttachmentDownloadData>.NotFound(loc["AttachmentNotFound"]);

        var data = new AttachmentDownloadData(
            attachment.Content, attachment.MimeType, attachment.FilenameOriginal, attachment.FileHash, attachment.UpdatedAt);

        return Result<AttachmentDownloadData>.Success(data);
    }

    public async Task<Result> DeleteAttachmentAsync(string groupId, string expenseId, string attachmentId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result.BadRequest(loc["InvalidExpenseIdFormat"]);

        if (!Guid.TryParse(attachmentId, out var attachmentGuid))
            return Result.BadRequest(loc["InvalidAttachmentIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result.Forbidden(loc["AccessNotAllowed"]);

        var expense = await unitOfWork.Expenses
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result.NotFound(loc["ExpenseNotFound"]);

        var attachment = await unitOfWork.ExpenseAttachments
            .FirstOrDefaultAsync(a => a.Guid == attachmentGuid && a.ExpenseId == expense.Id);

        if (attachment == null)
            return Result.NotFound(loc["AttachmentNotFound"]);

        unitOfWork.ExpenseAttachments.Remove(attachment);

        return Result.Success();
    }
}
