using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

namespace SplitDuo.Api.Services;

public interface IAuthenticationService
{
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request);
}

public class AuthenticationService(
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions) : IAuthenticationService
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

        var token = GenerateJwtToken(user);
        var refreshToken = Guid.NewGuid().ToString();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.Expires).ToUnixTimeSeconds();

        var authResponse = new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
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

    public async Task<Result<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request)
    {
        try
        {
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
            var userIdClaim = principal.FindFirst("userId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Result<AuthResponseDto>.Unauthorized("Invalid token");

            var user = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Guid == userId);

            if (user == null)
                return Result<AuthResponseDto>.NotFound("User not found");

            var newToken = GenerateJwtToken(user);
            var newRefreshToken = Guid.NewGuid().ToString();
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.Expires).ToUnixTimeSeconds();

            var authResponse = new AuthResponseDto
            {
                Token = newToken,
                RefreshToken = newRefreshToken,
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

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtOptions.SecretKey ?? "");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("userId", user.Guid.ToString()),
                new Claim("email", user.Email),
                new Claim("firstName", user.FirstName),
                new Claim("lastName", user.LastName)
            }),
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.Expires),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}