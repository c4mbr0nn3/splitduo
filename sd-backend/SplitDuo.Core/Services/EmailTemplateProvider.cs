using Microsoft.Extensions.Options;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;

namespace SplitDuo.Core.Services;

public interface IEmailTemplateProvider
{
    Notification Render(ITemplateModel model);
}

public class EmailTemplateProvider(IOptions<AppOptions> appOptions, TimeProvider timeProvider) : IEmailTemplateProvider
{
    private readonly AppOptions _appOptions = appOptions.Value;

    public Notification Render(ITemplateModel model)
    {
        model.Validate();
        return model.Template switch
        {
            EmailTemplate.PasswordReset => RenderPasswordReset((PasswordResetModel)model),
            EmailTemplate.PasswordResetSuccess => RenderPasswordResetSuccess((PasswordResetSuccessModel)model),
            EmailTemplate.PasswordChanged => RenderPasswordChanged((PasswordChangedModel)model),
            EmailTemplate.TwoFactorEnabled => RenderTwoFactorEnabled((TwoFactorEnabledModel)model),
            EmailTemplate.TwoFactorDisabled => RenderTwoFactorDisabled((TwoFactorDisabledModel)model),
            EmailTemplate.GroupInvitation => RenderGroupInvitation((GroupInvitationModel)model),
            EmailTemplate.GroupMemberAdded => RenderGroupMemberAdded((GroupMemberAddedModel)model),
            EmailTemplate.GroupDeleted => RenderGroupDeleted((GroupDeletedModel)model),
            EmailTemplate.GroupMemberRemoved => RenderGroupMemberRemoved((GroupMemberRemovedModel)model),
            EmailTemplate.ExpenseAdded => RenderExpenseAdded((ExpenseAddedModel)model),
            EmailTemplate.ExpenseDeleted => RenderExpenseDeleted((ExpenseDeletedModel)model),
            _ => throw new ArgumentOutOfRangeException(nameof(model.Template), model.Template, null)
        };
    }

    private Notification RenderPasswordReset(PasswordResetModel m)
    {
        var resetUrl =
            $"{_appOptions.BaseUrl}/reset-password?email={Uri.EscapeDataString(m.To)}&token={Uri.EscapeDataString(m.ResetToken)}";
        return new Notification
        {
            To = m.To,
            Subject = "Reset your SplitDuo password",
            Body = $"""
                    <p>Hi {m.FirstName},</p>
                    <p>We received a request to reset your password. Click the link below to choose a new one.</p>
                    <p><a href="{resetUrl}">Reset my password</a></p>
                    <p>This link expires in 1 hour and can only be used once.</p>
                    <p>Didn't request this? You can safely ignore this email — your password won't change.</p>
                    <p>— The SplitDuo Team</p>
                    """
        };
    }

    private static Notification RenderPasswordResetSuccess(PasswordResetSuccessModel m)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        return new Notification
        {
            To = m.To,
            Subject = "Your SplitDuo password has been reset",
            Body = $"""
                    <p>Hi {m.FirstName},</p>
                    <p>Your password was successfully reset. All active sessions have been signed out.</p>
                    <p>Account: {m.To}<br>
                    Time: {timestamp}</p>
                    <p>Didn't do this? Contact support immediately — your account may be at risk.</p>
                    <p>— The SplitDuo Team</p>
                    """
        };
    }

    private Notification RenderPasswordChanged(PasswordChangedModel m)
    {
        var timestamp = timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        return new Notification
        {
            To = m.To,
            Subject = "Your SplitDuo password was changed",
            Body = $"""
                    <p>Hi {m.FirstName},</p>
                    <p>Your password was changed. All active sessions have been signed out.</p>
                    <p>Account: {m.To}<br>
                    Time: {timestamp}</p>
                    <p>Didn't make this change? Contact support immediately — your account may be at risk.</p>
                    <p>— The SplitDuo Team</p>
                    """
        };
    }

    private static Notification RenderTwoFactorEnabled(TwoFactorEnabledModel m) =>
        new()
        {
            To = m.To,
            Subject = "Two-factor authentication is now active",
            Body = $"<p>Hi {m.FirstName},</p>" +
                   "<p>Two-factor authentication has been enabled on your account. You'll be asked for a verification code each time you sign in.</p>" +
                   "<p>Didn't do this? Contact support immediately — your account may be at risk.</p>" +
                   "<p>— The SplitDuo Team</p>"
        };

    private static Notification RenderTwoFactorDisabled(TwoFactorDisabledModel m) =>
        new()
        {
            To = m.To,
            Subject = "Two-factor authentication has been turned off",
            Body = $"<p>Hi {m.FirstName},</p>" +
                   "<p>Two-factor authentication has been disabled on your account. Consider re-enabling it for better protection.</p>" +
                   "<p>Didn't do this? Contact support immediately — your account may be at risk.</p>" +
                   "<p>— The SplitDuo Team</p>"
        };

    private Notification RenderGroupInvitation(GroupInvitationModel m)
    {
        var acceptUrl = $"{_appOptions.BaseUrl}/invite/accept?token={Uri.EscapeDataString(m.RawToken)}";
        return new Notification
        {
            To = m.To,
            Subject = $"{m.InviterFirstName} invited you to join {m.GroupName} on SplitDuo",
            Body = $"""
                    <p>Hi there,</p>
                    <p>{m.InviterFirstName} {m.InviterLastName} has invited you to split expenses in {m.GroupName} on SplitDuo.</p>
                    <p><a href="{acceptUrl}">Accept invitation</a></p>
                    <p>This invitation expires in 48 hours.</p>
                    <p>Not expecting this? You can safely ignore this email.</p>
                    <p>— The SplitDuo Team</p>
                    """
        };
    }

    private Notification RenderGroupMemberAdded(GroupMemberAddedModel m)
    {
        var groupUrl = $"{_appOptions.BaseUrl}/groups/{m.GroupGuid}";
        return new Notification
        {
            To = m.To,
            Subject = $"You've been added to {m.GroupName}",
            Body = $"""
                    <p>Hi {m.RecipientFirstName},</p>
                    <p>{m.AddedByFirstName} {m.AddedByLastName} added you to {m.GroupName} on SplitDuo.</p>
                    <p><a href="{groupUrl}">View group</a></p>
                    <p>— The SplitDuo Team</p>
                    """
        };
    }

    private static Notification RenderGroupDeleted(GroupDeletedModel m) =>
        new()
        {
            To = m.To,
            Subject = $"{m.GroupName} has been deleted",
            Body = $"""
                    <p>Hi {m.RecipientFirstName},</p>
                    <p>{m.DeletedByFirstName} {m.DeletedByLastName} deleted the group <strong>{m.GroupName}</strong>. All shared expenses and history for this group are no longer accessible.</p>
                    <p>— The SplitDuo Team</p>
                    """
        };

    private static Notification RenderGroupMemberRemoved(GroupMemberRemovedModel m) =>
        new()
        {
            To = m.To,
            Subject = $"You've been removed from {m.GroupName}",
            Body = $"""
                    <p>Hi {m.RecipientFirstName},</p>
                    <p>{m.RemovedByFirstName} {m.RemovedByLastName} removed you from <strong>{m.GroupName}</strong>. You no longer have access to this group's expenses.</p>
                    <p>— The SplitDuo Team</p>
                    """
        };

    private Notification RenderExpenseAdded(ExpenseAddedModel m)
    {
        var groupUrl = $"{_appOptions.BaseUrl}/groups/{m.GroupGuid}";
        return new Notification
        {
            To = m.To,
            Subject = $"{m.AddedByFirstName} added an expense to {m.GroupName}",
            Body = $"""
                    <p>Hi {m.RecipientFirstName},</p>
                    <p>{m.AddedByFirstName} {m.AddedByLastName} added a new expense to <strong>{m.GroupName}</strong>:</p>
                    <p><strong>{m.ExpenseTitle}</strong> &mdash; {m.ExpenseAmount:F2} &middot; {m.ExpenseDate:MMMM d, yyyy}</p>
                    <p><a href="{groupUrl}">View group</a></p>
                    <p>— The SplitDuo Team</p>
                    """
        };
    }

    private Notification RenderExpenseDeleted(ExpenseDeletedModel m)
    {
        var groupUrl = $"{_appOptions.BaseUrl}/groups/{m.GroupGuid}";
        return new Notification
        {
            To = m.To,
            Subject = $"An expense was removed from {m.GroupName}",
            Body = $"""
                    <p>Hi {m.RecipientFirstName},</p>
                    <p>{m.DeletedByFirstName} {m.DeletedByLastName} removed an expense from <strong>{m.GroupName}</strong>:</p>
                    <p><strong>{m.ExpenseTitle}</strong> &mdash; {m.ExpenseAmount:F2} &middot; {m.ExpenseDate:MMMM d, yyyy}</p>
                    <p><a href="{groupUrl}">View group</a></p>
                    <p>— The SplitDuo Team</p>
                    """
        };
    }
}