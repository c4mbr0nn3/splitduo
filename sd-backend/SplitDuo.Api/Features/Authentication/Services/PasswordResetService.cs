using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Localization;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Authentication.Services;

public interface IPasswordResetService
{
    Task<Result> InitiatePasswordResetAsync(string email);
    Task<Result<bool>> ValidateResetTokenAsync(string? token);
    Task<Result> ResetPasswordAsync(ResetPasswordRequestDto request);
}

public class PasswordResetService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    ITokenGenerator tokenGenerator,
    IAuthenticationService authenticationService,
    INotificationService notificationService,
    IEmailTemplateProvider emailTemplateProvider,
    TimeProvider timeProvider,
    IStringLocalizer<PasswordResetService> loc) : IPasswordResetService
{
    public async Task<Result> InitiatePasswordResetAsync(string email)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null);

        // Always return success to prevent email enumeration
        if (user == null)
            return Result.Success();

        // Generate secure random token
        var resetToken = tokenGenerator.GenerateSecureRandomToken();
        var hashedToken = HashUtils.Sha256Base64(resetToken);

        // Invalidate any existing password reset tokens for this user
        var existingTokens = await unitOfWork.TwoFactorTokens
            .Where(t => t.UserId == user.Id && t.Purpose == "password_reset" && t.UsedAt == null)
            .ToListAsync();

        foreach (var token in existingTokens)
        {
            token.UsedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        }

        // Create new password reset token
        var resetTokenEntity = new TwoFactorToken
        {
            UserId = user.Id,
            TokenHash = hashedToken,
            TokenType = "password_reset",
            Purpose = "password_reset",
            ExpiresAt = timeProvider.GetUtcNow().AddHours(1).ToUnixTimeSeconds(), // 1 hour expiration
            ClientInfo = "Password Reset"
        };

        unitOfWork.TwoFactorTokens.Add(resetTokenEntity);

        // Send password reset email
        // Use the user's UiLanguage for the email language (AC-005).
        // This is an unauthenticated flow, but we have the user record so we prefer
        // their saved language over the request culture (see spec §9.4).
        var userLanguage = SupportedLanguages.Normalize(user.Settings.UiLanguage);
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new PasswordResetModel
        {
            To = user.Email, FirstName = user.FirstName, ResetToken = resetToken
        }, userLanguage));

        return Result.Success();
    }

    public async Task<Result<bool>> ValidateResetTokenAsync(string? token)
    {
        var resetToken = await FindActiveResetTokenAsync(token);

        // One generic failure for every token problem: missing, malformed, unknown,
        // expired, used, or owned by a deleted user. A distinct message here told an
        // anonymous caller whether the submitted email belonged to an account
        // (CWE-204); the token is now the only authority.
        if (resetToken is null || resetToken.User.DeletedAt is not null)
            return Result<bool>.BadRequest(loc["InvalidOrExpiredResetToken"]);

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Resolves an active password-reset token from its hash alone. Expiry and
    /// single-use are part of the lookup so every inactive token is indistinguishable
    /// from an unknown one. The owning user is loaded through the token; the caller
    /// still rejects a soft-deleted owner.
    /// </summary>
    private async Task<TwoFactorToken?> FindActiveResetTokenAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hashedToken = HashUtils.Sha256Base64(token);
        var currentTimestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        return await unitOfWork.TwoFactorTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == hashedToken &&
                t.Purpose == "password_reset" &&
                t.UsedAt == null &&
                t.ExpiresAt >= currentTimestamp);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var resetToken = await FindActiveResetTokenAsync(request.Token);

        // Token-only resolution: the submitted email is accepted for the existing
        // API contract and rate-limit partitioning, but it must not decide whether
        // the account exists (CWE-204). Every failure is the same generic error.
        if (resetToken is null || resetToken.User.DeletedAt is not null)
            return Result.BadRequest(loc["InvalidOrExpiredResetToken"]);

        var user = resetToken.User;
        var currentTimestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        // Mark token as used
        resetToken.UsedAt = currentTimestamp;

        // Update user password
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);

        // Invalidate all existing tokens by rotating security stamp
        user.SecurityStamp = Guid.CreateVersion7().ToString();

        // Clear any account lockout from previous failed attempts
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        // Revoke all refresh tokens (force logout on all devices)
        await authenticationService.RevokeAllUserTokensAsync(user.Id, "Password reset");

        // Send password reset success email
        // Use the user's UiLanguage for the email language (AC-005).
        var userLanguage = SupportedLanguages.Normalize(user.Settings.UiLanguage);
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new PasswordResetSuccessModel
        {
            To = user.Email, FirstName = user.FirstName
        }, userLanguage));

        return Result.Success();
    }
}
