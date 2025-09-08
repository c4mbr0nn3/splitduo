using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
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
    Task<Result> RevokeAllUserTokensAsync(string userGuid);
}

public class AuthenticationService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    ITwoFactorService twoFactorService) : IAuthenticationService
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
            // Generate and send email code for 2FA
            await twoFactorService.GenerateEmailCodeAsync(user.Guid, "2fa_login");
            
            return Result<AuthResponseDto>.Success(new AuthResponseDto
            {
                RequiresTwoFactor = true,
                User = new UserInfoDto
                {
                    Id = user.Guid.ToString(),
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                }
            });
        }

        return await CompleteLoginAsync(user);
    }

    public async Task<Result<AuthResponseDto>> VerifyTwoFactorAndCompleteLoginAsync(VerifyTwoFactorLoginDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.DeletedAt == null);

        if (user == null)
            return Result<AuthResponseDto>.NotFound("User not found");

        if (!user.TwoFactorEnabled)
            return Result<AuthResponseDto>.BadRequest("Two-factor authentication is not enabled for this user");

        bool isValidCode = false;

        switch (request.CodeType.ToLower())
        {
            case "totp":
                var totpResult = await twoFactorService.ValidateTotpCodeAsync(user.Guid, request.Code);
                if (totpResult.IsFailure) return totpResult.MapTo<AuthResponseDto>();
                isValidCode = totpResult.Value;
                break;

            case "email":
                var emailResult = await twoFactorService.ValidateEmailCodeAsync(user.Guid, request.Code, "2fa_login");
                if (emailResult.IsFailure) return emailResult.MapTo<AuthResponseDto>();
                isValidCode = emailResult.Value;
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
            User = new UserInfoDto
            {
                Id = user.Guid.ToString(),
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            }
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
                User = new UserInfoDto
                {
                    Id = user.Guid.ToString(),
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                }
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
}