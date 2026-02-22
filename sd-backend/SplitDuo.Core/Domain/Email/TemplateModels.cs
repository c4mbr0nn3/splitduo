using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Domain.Email;

public interface ITemplateModel
{
    EmailTemplate Template { get; }
    void Validate();
}

public record PasswordResetModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.PasswordReset;
    public required string To { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string ResetToken { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(FirstName)) throw new ArgumentException("FirstName is required");
        if (string.IsNullOrWhiteSpace(ResetToken)) throw new ArgumentException("ResetToken is required");
    }
}

public record PasswordResetSuccessModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.PasswordResetSuccess;
    public required string To { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(FirstName)) throw new ArgumentException("FirstName is required");
    }
}

public record PasswordChangedModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.PasswordChanged;
    public required string To { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(FirstName)) throw new ArgumentException("FirstName is required");
    }
}

public record TwoFactorEnabledModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.TwoFactorEnabled;
    public required string To { get; init; }
    public required string FirstName { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(FirstName)) throw new ArgumentException("FirstName is required");
    }
}

public record TwoFactorDisabledModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.TwoFactorDisabled;
    public required string To { get; init; }
    public required string FirstName { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(FirstName)) throw new ArgumentException("FirstName is required");
    }
}

public record TwoFactorEmailCodeModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.TwoFactorEmailCode;
    public required string To { get; init; }
    public required string FirstName { get; init; }
    public required string Code { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(Code)) throw new ArgumentException("Code is required");
    }
}

public record GroupInvitationModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.GroupInvitation;
    public required string To { get; init; }
    public required string GroupName { get; init; }
    public required string InviterFirstName { get; init; }
    public required string InviterLastName { get; init; }
    public required string RawToken { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(GroupName)) throw new ArgumentException("GroupName is required");
        if (string.IsNullOrWhiteSpace(RawToken)) throw new ArgumentException("RawToken is required");
    }
}

public record GroupMemberAddedModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.GroupMemberAdded;
    public required string To { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string AddedByFirstName { get; init; }
    public required string AddedByLastName { get; init; }
    public required string GroupName { get; init; }
    public required Guid GroupGuid { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(GroupName)) throw new ArgumentException("GroupName is required");
        if (GroupGuid == Guid.Empty) throw new ArgumentException("GroupGuid is required");
    }
}

public record GroupDeletedModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.GroupDeleted;
    public required string To { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string DeletedByFirstName { get; init; }
    public required string DeletedByLastName { get; init; }
    public required string GroupName { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(GroupName)) throw new ArgumentException("GroupName is required");
    }
}

public record GroupMemberRemovedModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.GroupMemberRemoved;
    public required string To { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string RemovedByFirstName { get; init; }
    public required string RemovedByLastName { get; init; }
    public required string GroupName { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(GroupName)) throw new ArgumentException("GroupName is required");
    }
}

public record ExpenseAddedModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.ExpenseAdded;
    public required string To { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string AddedByFirstName { get; init; }
    public required string AddedByLastName { get; init; }
    public required string GroupName { get; init; }
    public required Guid GroupGuid { get; init; }
    public required string ExpenseTitle { get; init; }
    public required decimal ExpenseAmount { get; init; }
    public required DateOnly ExpenseDate { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(GroupName)) throw new ArgumentException("GroupName is required");
        if (GroupGuid == Guid.Empty) throw new ArgumentException("GroupGuid is required");
        if (string.IsNullOrWhiteSpace(ExpenseTitle)) throw new ArgumentException("ExpenseTitle is required");
        if (ExpenseAmount <= 0) throw new ArgumentException("ExpenseAmount must be positive");
    }
}

public record ExpenseDeletedModel : ITemplateModel
{
    public EmailTemplate Template => EmailTemplate.ExpenseDeleted;
    public required string To { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string DeletedByFirstName { get; init; }
    public required string DeletedByLastName { get; init; }
    public required string GroupName { get; init; }
    public required Guid GroupGuid { get; init; }
    public required string ExpenseTitle { get; init; }
    public required decimal ExpenseAmount { get; init; }
    public required DateOnly ExpenseDate { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(To)) throw new ArgumentException("To is required");
        if (string.IsNullOrWhiteSpace(GroupName)) throw new ArgumentException("GroupName is required");
        if (GroupGuid == Guid.Empty) throw new ArgumentException("GroupGuid is required");
        if (string.IsNullOrWhiteSpace(ExpenseTitle)) throw new ArgumentException("ExpenseTitle is required");
        if (ExpenseAmount <= 0) throw new ArgumentException("ExpenseAmount must be positive");
    }
}