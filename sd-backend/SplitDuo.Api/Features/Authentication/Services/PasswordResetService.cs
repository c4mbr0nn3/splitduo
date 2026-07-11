using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Authentication.Services;

public interface IPasswordResetService
{
    Task<Result> InitiatePasswordResetAsync(string email);
    Task<Result<bool>> ValidateResetTokenAsync(string email, string token);
    Task<Result> ResetPasswordAsync(ResetPasswordRequestDto request);
}

public class PasswordResetService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    ITokenGenerator tokenGenerator,
    IAuthenticationService authenticationService,
    INotificationService notificationService,
    IEmailTemplateProvider emailTemplateProvider) : IPasswordResetService
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
            token.UsedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // Create new password reset token
        var resetTokenEntity = new TwoFactorToken
        {
            UserId = user.Id,
            TokenHash = hashedToken,
            TokenType = "password_reset",
            Purpose = "password_reset",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), // 1 hour expiration
            MaxAttempts = 3,
            ClientInfo = "Password Reset"
        };

        unitOfWork.TwoFactorTokens.Add(resetTokenEntity);

        // Send password reset email
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new PasswordResetModel
        {
            To = user.Email, FirstName = user.FirstName, ResetToken = resetToken
        }));

        return Result.Success();
    }

    public async Task<Result<bool>> ValidateResetTokenAsync(string email, string token)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null);

        if (user == null)
            return Result<bool>.BadRequest("Invalid email or token");

        var hashedToken = HashUtils.Sha256Base64(token);
        var resetToken = await unitOfWork.TwoFactorTokens
            .FirstOrDefaultAsync(t =>
                t.UserId == user.Id &&
                t.TokenHash == hashedToken &&
                t.Purpose == "password_reset" &&
                t.UsedAt == null);

        if (resetToken == null)
            return Result<bool>.BadRequest("Invalid or expired reset token");

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Check if token is expired
        if (resetToken.ExpiresAt < currentTimestamp)
            return Result<bool>.BadRequest("Reset token has expired");

        // Check if max attempts reached
        if (resetToken.Attempts >= resetToken.MaxAttempts)
            return Result<bool>.BadRequest("Maximum validation attempts exceeded");

        // Increment attempts
        resetToken.Attempts++;

        return Result<bool>.Success(true);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.DeletedAt == null);

        if (user == null)
            return Result.BadRequest("Invalid email or token");

        var hashedToken = HashUtils.Sha256Base64(request.Token);
        var resetToken = await unitOfWork.TwoFactorTokens
            .FirstOrDefaultAsync(t =>
                t.UserId == user.Id &&
                t.TokenHash == hashedToken &&
                t.Purpose == "password_reset" &&
                t.UsedAt == null);

        if (resetToken == null)
            return Result.BadRequest("Invalid or expired reset token");

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Check if token is expired
        if (resetToken.ExpiresAt < currentTimestamp)
            return Result.BadRequest("Reset token has expired");

        // Check if max attempts reached
        if (resetToken.Attempts >= resetToken.MaxAttempts)
            return Result.BadRequest("Maximum validation attempts exceeded");

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
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new PasswordResetSuccessModel
        {
            To = user.Email, FirstName = user.FirstName
        }));

        return Result.Success();
    }
}
