using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Localization;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Users.Services;

public interface IEmailChangeService
{
    Task<Result> RequestChangeAsync(Guid targetUserId, Guid actingAdminId, ChangeEmailRequestDto request);
    Task<Result> ConfirmChangeAsync(string token);
}

public class EmailChangeService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    ITokenGenerator tokenGenerator,
    IAuthenticationService authenticationService,
    INotificationService notificationService,
    IEmailTemplateProvider emailTemplateProvider,
    TimeProvider timeProvider,
    IStringLocalizer<EmailChangeService> loc,
    ILogger<EmailChangeService> logger) : IEmailChangeService
{
    private const int TokenLifetimeSeconds = 3600;

    public async Task<Result> RequestChangeAsync(
        Guid targetUserId, Guid actingAdminId, ChangeEmailRequestDto request)
    {
        var actingAdmin = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == actingAdminId && u.DeletedAt == null);

        if (actingAdmin == null)
            return Result.Unauthorized(loc["UserNotAuthenticated"]);

        // The admin session alone is not enough: a stolen session could otherwise
        // point a victim's address at a mailbox the attacker controls.
        var verification = passwordHasher.VerifyHashedPassword(
            actingAdmin, actingAdmin.PasswordHash, request.CurrentPassword);

        if (verification == PasswordVerificationResult.Failed)
            return Result.Unauthorized(loc["CurrentPasswordIncorrect"]);

        var target = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == targetUserId && u.DeletedAt == null);

        if (target == null)
            return Result.NotFound(loc["UserNotFound"]);

        var newEmail = EmailNormalizer.Normalize(request.NewEmail);

        // No change and no email: 204 keeps the API from claiming a verification
        // link was sent. The admin UI already disables the submit button for this case.
        if (newEmail == target.Email)
            return Result.Success(HttpStatusCode.NoContent);

        var taken = await unitOfWork.Users.AnyAsync(u =>
            u.Id != target.Id && u.DeletedAt == null && u.Email == newEmail);

        if (taken)
            return Result.Conflict(loc["EmailAlreadyExists"]);

        var token = tokenGenerator.GenerateSecureRandomToken();
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        // A new request replaces any previous pending change; only the newest link works.
        target.PendingEmail = newEmail;
        target.PendingEmailTokenHash = HashUtils.Sha256Base64(token);
        target.PendingEmailExpiresAt = now + TokenLifetimeSeconds;

        var language = SupportedLanguages.Normalize(target.Settings.UiLanguage);
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(
            new EmailChangeVerificationModel
            {
                To = newEmail,
                FirstName = target.FirstName,
                NewEmail = newEmail,
                Token = token
            }, language));

        logger.LogInformation(
            "Email change requested by admin {AdminId} for user {UserId}: {OldEmail} -> {NewEmail}",
            actingAdmin.Id, target.Id, target.Email, newEmail);

        return Result.Success(HttpStatusCode.Accepted);
    }

    public async Task<Result> ConfirmChangeAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.BadRequest(loc["InvalidOrExpiredEmailChangeToken"]);

        var hash = HashUtils.Sha256Base64(token);
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        // Every failure below returns the same message: the token is the only
        // authority, and a distinct error would turn this into a state oracle.
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u =>
                u.PendingEmailTokenHash == hash &&
                u.DeletedAt == null &&
                u.PendingEmail != null &&
                u.PendingEmailExpiresAt >= now);

        if (user == null)
            return Result.BadRequest(loc["InvalidOrExpiredEmailChangeToken"]);

        var oldEmail = user.Email;
        var newEmail = user.PendingEmail!;

        // The address may have been taken between request and confirmation.
        // Clear the pending row so the admin can retry after choosing another address.
        var taken = await unitOfWork.Users.AnyAsync(u =>
            u.Id != user.Id && u.DeletedAt == null && u.Email == newEmail);

        if (taken)
        {
            ClearPendingChange(user);
            return Result.BadRequest(loc["InvalidOrExpiredEmailChangeToken"]);
        }

        user.Email = newEmail;
        ClearPendingChange(user);

        // Force a fresh login: the email is the login identity.
        user.SecurityStamp = Guid.CreateVersion7().ToString();

        var revokeResult = await authenticationService.RevokeAllUserTokensAsync(
            user.Id, "Email changed");

        if (revokeResult.IsFailure)
            logger.LogWarning(
                "Failed to revoke tokens for user {UserId} after email change: {Error}",
                user.Id, revokeResult.Error);

        // A reset link sent to the old mailbox must die with the old address.
        var passwordResetTokens = await unitOfWork.TwoFactorTokens
            .Where(t => t.UserId == user.Id && t.Purpose == "password_reset" && t.UsedAt == null)
            .ToListAsync();

        foreach (var resetToken in passwordResetTokens)
            resetToken.UsedAt = now;

        var language = SupportedLanguages.Normalize(user.Settings.UiLanguage);
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(
            new EmailChangedModel
            {
                To = oldEmail,
                FirstName = user.FirstName,
                NewEmail = newEmail
            }, language));

        logger.LogInformation(
            "Email change applied for user {UserId}: {OldEmail} -> {NewEmail}",
            user.Id, oldEmail, newEmail);

        return Result.Success();
    }

    private static void ClearPendingChange(User user)
    {
        user.PendingEmail = null;
        user.PendingEmailTokenHash = null;
        user.PendingEmailExpiresAt = null;
    }
}
