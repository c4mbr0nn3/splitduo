using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Authentication.Services;

public interface IAuthenticationService
{
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Result<AuthResponseDto>> VerifyTwoFactorAndCompleteLoginAsync(VerifyTwoFactorLoginDto request);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task<Result> RevokeRefreshTokenAsync(string refreshToken, Guid userGuid);
    Task<Result> RevokeAllUserTokensAsync(string userGuid);
    Task<Result> InitiatePasswordResetAsync(string email);
    Task<Result<bool>> ValidateResetTokenAsync(string email, string token);
    Task<Result> ResetPasswordAsync(ResetPasswordRequestDto request);
}

public class AuthenticationService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    ITwoFactorService twoFactorService,
    INotificationService notificationService,
    IEmailTemplateProvider emailTemplateProvider) : IAuthenticationService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            return Result<AuthResponseDto>.Unauthorized("Invalid email or password");

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
            return Result<AuthResponseDto>.Unauthorized("Invalid email or password");

        // Check if 2FA is enabled for this user
        if (user.TwoFactorEnabled)
        {
            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                RequiresTwoFactor = true,
                TwoFactorChallengeToken = GenerateChallengeToken(user.Guid),
                User = new UserDto(user)
            });
        }

        return await CompleteLoginAsync(user);
    }

    public async Task<Result<AuthResponseDto>> VerifyTwoFactorAndCompleteLoginAsync(VerifyTwoFactorLoginDto request)
    {
        // MapInboundClaims = false keeps JWT claim names as-is (e.g. "sub" stays "sub")
        // instead of mapping them to WS-Federation URIs (e.g. ClaimTypes.NameIdentifier)
        var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var key = Encoding.ASCII.GetBytes(_jwtOptions.SecretKey ?? "");
        ClaimsPrincipal principal;
        try
        {
            principal = tokenHandler.ValidateToken(request.ChallengeToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true, ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = true, ValidAudience = _jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
        }
        catch
        {
            return Result<AuthResponseDto>.Unauthorized("Invalid or expired challenge token");
        }

        if (principal.FindFirst("purpose")?.Value != "2fa_challenge")
            return Result<AuthResponseDto>.Unauthorized("Invalid challenge token");

        if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userGuid))
            return Result<AuthResponseDto>.Unauthorized("Invalid challenge token");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<AuthResponseDto>.NotFound("User not found");

        if (!user.TwoFactorEnabled)
            return Result<AuthResponseDto>.BadRequest("Two-factor authentication is not enabled for this user");

        var isValidCode = false;

        switch (request.CodeType.ToLower())
        {
            case "totp":
                var totpResult = await twoFactorService.ValidateTotpCodeAsync(user.Guid, request.Code);
                if (totpResult.IsFailure) return totpResult.MapTo<AuthResponseDto>();
                isValidCode = totpResult.Value;
                break;

            case "backup":
                var backupResult = await twoFactorService.ValidateBackupCodeAsync(user.Guid, request.Code);
                if (backupResult.IsFailure) return backupResult.MapTo<AuthResponseDto>();
                isValidCode = backupResult.Value;
                break;

            default:
                return Result<AuthResponseDto>.BadRequest("Invalid code type");
        }

        if (!isValidCode)
            return Result<AuthResponseDto>.Unauthorized("Invalid verification code");

        return await CompleteLoginAsync(user);
    }

    private async Task<Result<AuthResponseDto>> CompleteLoginAsync(User user)
    {
        var jwtId = Guid.CreateVersion7().ToString();
        var token = GenerateJwtToken(user, jwtId);
        var refreshTokenValue = GenerateSecureRefreshToken();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.Expires).ToUnixTimeSeconds();

        // Revoke all existing refresh tokens for this user (single session security)
        await RevokeAllUserTokensAsync(user.Id, "New login - previous sessions invalidated");

        // Store new refresh token in database
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshTokenValue),
            JwtId = jwtId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds(), // 7 days for refresh token
            ClientInfo = "API Client" // Could be enhanced to include actual client info
        };

        unitOfWork.RefreshTokens.Add(refreshToken);

        var authResponse = new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshTokenValue,
            ExpiresAt = expiresAt,
            RequiresTwoFactor = false,
            User = new UserDto(user)
        };

        return Result<AuthResponseDto>.Success(authResponse);
    }

    public async Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        try
        {
            // 1. Validate the expired JWT to extract claims
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtOptions.SecretKey ?? "");

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // Allow expired tokens for refresh
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out var validatedToken);
            var jwtIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var userIdClaim = principal.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(jwtIdClaim) || string.IsNullOrEmpty(userIdClaim) ||
                !Guid.TryParse(userIdClaim, out var userId))
                return Result<AuthResponseDto>.Unauthorized("Invalid token");

            // 2. Validate the refresh token exists and is active
            var refreshTokenHash = HashToken(request.RefreshToken);
            var storedRefreshToken = await unitOfWork.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash && rt.JwtId == jwtIdClaim);

            if (storedRefreshToken == null)
                return Result<AuthResponseDto>.Unauthorized("Invalid refresh token");

            // 3. Get user details first
            var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Guid == userId);
            if (user == null)
                return Result<AuthResponseDto>.NotFound("User not found");

            if (!storedRefreshToken.IsActive)
            {
                // If token is revoked or expired, revoke all tokens for this user (security measure)
                await RevokeAllUserTokensAsync(user.Id, "Refresh token reuse detected");
                return Result<AuthResponseDto>.Unauthorized("Refresh token is no longer valid");
            }

            // 4. Revoke the used refresh token (token rotation)
            storedRefreshToken.RevokedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            storedRefreshToken.RevokedReason = "Used for refresh";

            // 5. Generate new tokens
            var newJwtId = Guid.CreateVersion7().ToString();
            var newAccessToken = GenerateJwtToken(user, newJwtId);
            var newRefreshTokenValue = GenerateSecureRefreshToken();
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.Expires).ToUnixTimeSeconds();

            // 6. Store new refresh token
            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(newRefreshTokenValue),
                JwtId = newJwtId,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds(),
                ClientInfo = storedRefreshToken.ClientInfo
            };

            // Link the old token to the new one for audit trail
            storedRefreshToken.ReplacedByToken = HashToken(newRefreshTokenValue);

            unitOfWork.RefreshTokens.Add(newRefreshToken);

            var authResponse = new AuthResponseDto
            {
                Token = newAccessToken,
                RefreshToken = newRefreshTokenValue,
                ExpiresAt = expiresAt,
                User = new UserDto(user)
            };

            return Result<AuthResponseDto>.Success(authResponse);
        }
        catch (Exception)
        {
            return Result<AuthResponseDto>.Unauthorized("Invalid token");
        }
    }

    private string GenerateJwtToken(User user, string jwtId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtOptions.SecretKey ?? "");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Jti, jwtId),
                new Claim(JwtRegisteredClaimNames.Sub, user.Guid.ToString()),
                new Claim("userId", user.Guid.ToString()),
                new Claim("email", user.Email),
                new Claim("firstName", user.FirstName),
                new Claim("lastName", user.LastName),
                new Claim(ClaimTypes.Role, user.GlobalRoleId.ToString())
            ]),
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.Expires),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string GenerateChallengeToken(Guid userGuid)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtOptions.SecretKey ?? "");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, userGuid.ToString()),
                new Claim("purpose", "2fa_challenge"),
            ]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }

    private static string GenerateSecureRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashedBytes);
    }

    public async Task<Result> RevokeRefreshTokenAsync(string refreshToken, Guid userGuid)
    {
        var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);
        if (user == null) return Result.NotFound("User not found");

        var refreshTokenHash = HashToken(refreshToken);
        var storedRefreshToken = await unitOfWork.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash && rt.UserId == user.Id);

        if (storedRefreshToken == null)
            return Result.NotFound("Refresh token not found");

        if (storedRefreshToken.IsRevoked)
            return Result.BadRequest("Refresh token already revoked");

        storedRefreshToken.RevokedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        storedRefreshToken.RevokedReason = "User logout";

        return Result.Success();
    }

    public async Task<Result> RevokeAllUserTokensAsync(string userGuid)
    {
        if (!Guid.TryParse(userGuid, out var userId)) return Result.BadRequest("Invalid user guid");

        var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Guid == userId && u.DeletedAt == null);
        if (user == null) return Result.NotFound("User not found");

        await RevokeAllUserTokensAsync(user.Id, "All tokens revoked by system administrator");
        return Result.Success();
    }

    private async Task RevokeAllUserTokensAsync(int userId, string reason)
    {
        var activeTokens = await unitOfWork.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var token in activeTokens)
        {
            token.RevokedAt = currentTimestamp;
            token.RevokedReason = reason;
        }
    }

    public async Task<Result> InitiatePasswordResetAsync(string email)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null);

        // Always return success to prevent email enumeration
        if (user == null)
            return Result.Success();

        // Generate secure random token
        var resetToken = GenerateSecureRefreshToken();
        var hashedToken = HashToken(resetToken);

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

        var hashedToken = HashToken(token);
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

        var hashedToken = HashToken(request.Token);
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

        // Revoke all refresh tokens (force logout on all devices)
        await RevokeAllUserTokensAsync(user.Id, "Password reset");

        // Send password reset success email
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new PasswordResetSuccessModel
        {
            To = user.Email, FirstName = user.FirstName
        }));

        return Result.Success();
    }
}