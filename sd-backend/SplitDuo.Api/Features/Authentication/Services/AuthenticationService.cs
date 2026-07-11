using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Authentication.Services;

public interface IAuthenticationService
{
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Result<AuthResponseDto>> VerifyTwoFactorAndCompleteLoginAsync(VerifyTwoFactorLoginDto request);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request);
    Task<Result> RevokeRefreshTokenAsync(string refreshToken, Guid userGuid);
    Task<Result> RevokeAllUserTokensAsync(int userId, string reason);
}

public class AuthenticationService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    ITwoFactorService twoFactorService,
    ILogger<AuthenticationService> logger,
    ITokenGenerator tokenGenerator) : IAuthenticationService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            return Result<AuthResponseDto>.Unauthorized("Invalid email or password");

        // Check account lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return Result<AuthResponseDto>.Unauthorized("Account temporarily locked. Try again later.");

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
            return Result<AuthResponseDto>.Unauthorized("Invalid email or password");
        }

        // Reset lockout on successful login
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        // Check if 2FA is enabled for this user
        if (user.TwoFactorEnabled)
        {
            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                RequiresTwoFactor = true,
                TwoFactorChallengeToken = tokenGenerator.GenerateChallengeToken(user.Guid),
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
        // Reset lockout on successful login (covers 2FA path)
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        // Backfill security stamp for pre-migration users (migration default was empty string)
        if (string.IsNullOrEmpty(user.SecurityStamp))
            user.SecurityStamp = Guid.CreateVersion7().ToString();

        var jwtId = Guid.CreateVersion7().ToString();
        var token = tokenGenerator.GenerateJwtToken(user, jwtId);
        var refreshTokenValue = tokenGenerator.GenerateSecureRandomToken();
        var familyId = Guid.CreateVersion7().ToString();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.Expires).ToUnixTimeSeconds();

        // Store new refresh token in database
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashUtils.Sha256Base64(refreshTokenValue),
            JwtId = jwtId,
            FamilyId = familyId,
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
            var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
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
            var refreshTokenHash = HashUtils.Sha256Base64(request.RefreshToken);
            var storedRefreshToken = await unitOfWork.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash && rt.JwtId == jwtIdClaim);

            if (storedRefreshToken == null)
                return Result<AuthResponseDto>.Unauthorized("Invalid refresh token");

            // 3. Get user details first
            var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Guid == userId && u.DeletedAt == null);
            if (user == null)
                return Result<AuthResponseDto>.NotFound("User not found");

            // Verify security stamp
            var tokenSecurityStamp = principal.FindFirst("security_stamp")?.Value;
            if (string.IsNullOrEmpty(tokenSecurityStamp) || tokenSecurityStamp != user.SecurityStamp)
            {
                await RevokeTokenChainAsync(user.Id, storedRefreshToken.FamilyId, "Security stamp mismatch");
                return Result<AuthResponseDto>.Unauthorized("Token is no longer valid");
            }

            if (!storedRefreshToken.IsActive)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var withinGraceWindow = storedRefreshToken.RevokedAt.HasValue
                    && storedRefreshToken.RevokedReason == "Used for refresh"
                    && now - storedRefreshToken.RevokedAt.Value <= 30;

                if (!withinGraceWindow)
                {
                    // If token is revoked or expired outside grace window, revoke the chain (security measure)
                    await RevokeTokenChainAsync(user.Id, storedRefreshToken.FamilyId, "Refresh token reuse detected");
                    return Result<AuthResponseDto>.Unauthorized("Refresh token is no longer valid");
                }
                // Grace window: treat consumed token as valid, continue to issue new tokens
            }

            // 4. Revoke the used refresh token (token rotation)
            storedRefreshToken.RevokedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            storedRefreshToken.RevokedReason = "Used for refresh";

            // 5. Generate new tokens
            var newJwtId = Guid.CreateVersion7().ToString();
            var newAccessToken = tokenGenerator.GenerateJwtToken(user, newJwtId);
            var newRefreshTokenValue = tokenGenerator.GenerateSecureRandomToken();
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.Expires).ToUnixTimeSeconds();

            // 6. Store new refresh token
            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashUtils.Sha256Base64(newRefreshTokenValue),
                JwtId = newJwtId,
                FamilyId = storedRefreshToken.FamilyId,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds(),
                ClientInfo = storedRefreshToken.ClientInfo
            };

            // Link the old token to the new one for audit trail
            storedRefreshToken.ReplacedByToken = HashUtils.Sha256Base64(newRefreshTokenValue);

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during token refresh");
            return Result<AuthResponseDto>.Unauthorized("Invalid token");
        }
    }

    public async Task<Result> RevokeRefreshTokenAsync(string refreshToken, Guid userGuid)
    {
        var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);
        if (user == null) return Result.NotFound("User not found");

        var refreshTokenHash = HashUtils.Sha256Base64(refreshToken);
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

    public async Task<Result> RevokeAllUserTokensAsync(int userId, string reason)
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

        return Result.Success();
    }

    private async Task RevokeTokenChainAsync(int userId, string familyId, string reason)
    {
        var chainTokens = await unitOfWork.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.FamilyId == familyId && rt.RevokedAt == null)
            .ToListAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var token in chainTokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
        }
    }
}