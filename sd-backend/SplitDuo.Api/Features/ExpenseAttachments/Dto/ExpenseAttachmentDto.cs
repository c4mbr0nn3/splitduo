using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Api.Features.ExpenseAttachments.Dto;

public class ExpenseAttachmentDto
{
    public string Id { get; set; } = "";           // Guid.ToString()
    public string ExpenseId { get; set; } = "";     // Guid.ToString()
    public string FilenameOriginal { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long SizeBytes { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }

    public ExpenseAttachmentDto()
    {
    }

    public ExpenseAttachmentDto(ExpenseAttachment attachment)
    {
        Id = attachment.Guid.ToString();
        ExpenseId = attachment.Expense.Guid.ToString();
        FilenameOriginal = attachment.FilenameOriginal;
        MimeType = attachment.MimeType;
        SizeBytes = attachment.SizeBytes;
        CreatedAt = attachment.CreatedAt;
        UpdatedAt = attachment.UpdatedAt;
    }

    public ExpenseAttachmentDto(ExpenseAttachment attachment, string expenseGuid)
    {
        Id = attachment.Guid.ToString();
        ExpenseId = expenseGuid;
        FilenameOriginal = attachment.FilenameOriginal;
        MimeType = attachment.MimeType;
        SizeBytes = attachment.SizeBytes;
        CreatedAt = attachment.CreatedAt;
        UpdatedAt = attachment.UpdatedAt;
    }
}
