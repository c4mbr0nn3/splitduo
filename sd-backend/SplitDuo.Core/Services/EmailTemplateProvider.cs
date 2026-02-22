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
            EmailTemplate.TwoFactorEmailCode => RenderTwoFactorEmailCode((TwoFactorEmailCodeModel)model),
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
            Subject = "SplitDuo - Password Reset Request",
            Body = $"""
                    <p>Hello {m.FirstName} {m.LastName},</p>
                    <p>We received a request to reset your SplitDuo account password.</p>
                    <p>To reset your password, click the link below:</p>
                    <p><a href="{resetUrl}">Reset Password</a></p>
                    <p><strong>This link will expire in 1 hour.</strong></p>
                    <p><strong>Important:</strong> If you did not request this password reset, please ignore this email. Your password will remain unchanged.</p>
                    <p>For security reasons, this link can only be used once.</p>
                    <p>Best regards,<br>
                    The SplitDuo Team</p>
                    """
        };
    }

    private static Notification RenderPasswordResetSuccess(PasswordResetSuccessModel m)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        return new Notification
        {
            To = m.To,
            Subject = "SplitDuo - Password Successfully Reset",
            Body = $"""
                    <p>Hello {m.FirstName} {m.LastName},</p>
                    <p>Your SplitDuo account password has been successfully reset.</p>
                    <p><strong>Reset Details:</strong><br>
                    Email: {m.To}<br>
                    Date &amp; Time: {timestamp}</p>
                    <p><strong>Security Notice:</strong> For your security, all active sessions have been logged out. Please log in with your new password.</p>
                    <p><strong>Didn't make this change?</strong><br>
                    If you did not reset your password, please contact support immediately as your account may be compromised.</p>
                    <p>Best regards,<br>
                    The SplitDuo Team</p>
                    """
        };
    }

    private Notification RenderPasswordChanged(PasswordChangedModel m)
    {
        var timestamp = timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        return new Notification
        {
            To = m.To,
            Subject = "SplitDuo - Password Changed",
            Body = $"""
                    <p>Hello {m.FirstName} {m.LastName},</p>
                    <p>This is a security notification to inform you that your SplitDuo account password was successfully changed.</p>
                    <p><strong>Change Details:</strong><br>
                    Email: {m.To}<br>
                    Date &amp; Time: {timestamp}</p>
                    <p><strong>Security Notice:</strong> For your security, all active sessions have been logged out. You will need to log in again with your new password.</p>
                    <p><strong>Didn't make this change?</strong><br>
                    If you did not request this password change, please contact support immediately as your account may be compromised.</p>
                    <p>Best regards,<br>
                    The SplitDuo Team</p>
                    """
        };
    }

    private static Notification RenderTwoFactorEnabled(TwoFactorEnabledModel m) =>
        new()
        {
            To = m.To,
            Subject = "Two-Factor Authentication Enabled",
            Body = $"<p>Hello {m.FirstName},</p>" +
                   "<p>Two-factor authentication has been successfully enabled on your SplitDuo account.</p>" +
                   "<p>Your account is now more secure. You will need to provide a verification code when logging in.</p>" +
                   "<p>If you did not enable this feature, please contact support immediately.</p>"
        };

    private static Notification RenderTwoFactorDisabled(TwoFactorDisabledModel m) =>
        new()
        {
            To = m.To,
            Subject = "Two-Factor Authentication Disabled",
            Body = $"<p>Hello {m.FirstName},</p>" +
                   "<p>Two-factor authentication has been disabled on your SplitDuo account.</p>" +
                   "<p>Your account security has been reduced. Consider re-enabling 2FA for better protection.</p>" +
                   "<p>If you did not disable this feature, please contact support immediately and secure your account.</p>"
        };

    private static Notification RenderTwoFactorEmailCode(TwoFactorEmailCodeModel m) =>
        new()
        {
            To = m.To,
            Subject = "Your SplitDuo Verification Code",
            Body = $"<p>Hello {m.FirstName},</p>" +
                   $"<p>Your verification code is: <strong>{m.Code}</strong></p>" +
                   "<p>This code will expire in 10 minutes.</p>" +
                   "<p>If you did not request this code, please ignore this email.</p>"
        };

    private Notification RenderGroupInvitation(GroupInvitationModel m)
    {
        var acceptUrl = $"{_appOptions.BaseUrl}/invite/accept?token={Uri.EscapeDataString(m.RawToken)}";
        return new Notification
        {
            To = m.To,
            Subject = $"You've been invited to join {m.GroupName} on SplitDuo",
            Body = $"""
                    <p>Hello,</p>
                    <p>{m.InviterFirstName} {m.InviterLastName} has invited you to join the group "{m.GroupName}" on SplitDuo.</p>
                    <p>To get started, create your account by clicking the link below:</p>
                    <p><a href="{acceptUrl}">Create Account</a></p>
                    <p><strong>This link will expire in 48 hours.</strong></p>
                    <p>If you did not expect this invitation, you can safely ignore this email.</p>
                    <p>Best regards,<br>The SplitDuo Team</p>
                    """
        };
    }

    private Notification RenderGroupMemberAdded(GroupMemberAddedModel m)
    {
        var groupUrl = $"{_appOptions.BaseUrl}/groups/{m.GroupGuid}";
        return new Notification
        {
            To = m.To,
            Subject = $"You've been added to {m.GroupName} on SplitDuo",
            Body = $"""
                    <p>Hello {m.RecipientFirstName},</p>
                    <p>{m.AddedByFirstName} {m.AddedByLastName} has added you to the group "{m.GroupName}" on SplitDuo.</p>
                    <p>You can view the group here:</p>
                    <p><a href="{groupUrl}">View Group</a></p>
                    <p>Best regards,<br>The SplitDuo Team</p>
                    """
        };
    }

    private static Notification RenderGroupDeleted(GroupDeletedModel m) =>
        new()
        {
            To = m.To,
            Subject = $"{m.GroupName} has been deleted",
            Body = $"""
                    <p>Hello {m.RecipientFirstName},</p>
                    <p>{m.DeletedByFirstName} {m.DeletedByLastName} has deleted the group <strong>{m.GroupName}</strong>.</p>
                    <p>All expenses and history for this group are no longer accessible.</p>
                    <p>Best regards,<br>The SplitDuo Team</p>
                    """
        };

    private static Notification RenderGroupMemberRemoved(GroupMemberRemovedModel m) =>
        new()
        {
            To = m.To,
            Subject = $"You've been removed from {m.GroupName}",
            Body = $"""
                    <p>Hello {m.RecipientFirstName},</p>
                    <p>{m.RemovedByFirstName} {m.RemovedByLastName} has removed you from the group <strong>{m.GroupName}</strong>.</p>
                    <p>You no longer have access to this group's expenses.</p>
                    <p>Best regards,<br>The SplitDuo Team</p>
                    """
        };

    private Notification RenderExpenseAdded(ExpenseAddedModel m)
    {
        var groupUrl = $"{_appOptions.BaseUrl}/groups/{m.GroupGuid}";
        return new Notification
        {
            To = m.To,
            Subject = $"New expense in {m.GroupName}",
            Body = $"""
                    <p>Hello {m.RecipientFirstName},</p>
                    <p>{m.AddedByFirstName} {m.AddedByLastName} added a new expense to <strong>{m.GroupName}</strong>:</p>
                    <p><strong>{m.ExpenseTitle}</strong> &mdash; {m.ExpenseAmount:F2} on {m.ExpenseDate:yyyy-MM-dd}</p>
                    <p><a href="{groupUrl}">View group</a></p>
                    <p>Best regards,<br>The SplitDuo Team</p>
                    """
        };
    }

    private Notification RenderExpenseDeleted(ExpenseDeletedModel m)
    {
        var groupUrl = $"{_appOptions.BaseUrl}/groups/{m.GroupGuid}";
        return new Notification
        {
            To = m.To,
            Subject = $"Expense removed from {m.GroupName}",
            Body = $"""
                    <p>Hello {m.RecipientFirstName},</p>
                    <p>{m.DeletedByFirstName} {m.DeletedByLastName} removed an expense from <strong>{m.GroupName}</strong>:</p>
                    <p><strong>{m.ExpenseTitle}</strong> &mdash; {m.ExpenseAmount:F2} on {m.ExpenseDate:yyyy-MM-dd}</p>
                    <p><a href="{groupUrl}">View group</a></p>
                    <p>Best regards,<br>The SplitDuo Team</p>
                    """
        };
    }
}