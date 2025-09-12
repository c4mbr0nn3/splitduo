using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;
using SplitDuo.Api.Features.Authentication.Dto;

namespace SplitDuo.Api.Features.Authentication.Services;

public interface ITwoFactorService
{
    Task<Result<TwoFactorSetupDto>> InitiateSetupAsync(Guid userGuid);
    Task<Result> VerifySetupAsync(Guid userGuid, VerifyTwoFactorSetupDto request);
    Task<Result> DisableAsync(Guid userGuid, DisableTwoFactorDto request);
    Task<Result<string>> GenerateEmailCodeAsync(Guid userGuid, string purpose = "2fa_login");
    Task<Result<bool>> ValidateEmailCodeAsync(Guid userGuid, string code, string purpose = "2fa_login");
    Task<Result<bool>> ValidateTotpCodeAsync(Guid userGuid, string code);
    Task<Result<bool>> ValidateBackupCodeAsync(Guid userGuid, string code);
    Task<Result<List<string>>> GenerateBackupCodesAsync(Guid userGuid);
}

public class TwoFactorService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService) : ITwoFactorService
{
    public async Task<Result<TwoFactorSetupDto>> InitiateSetupAsync(Guid userGuid)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<TwoFactorSetupDto>.NotFound("User not found");

        if (user.TwoFactorEnabled)
            return Result<TwoFactorSetupDto>.BadRequest("Two-factor authentication is already enabled");

        var secret = GenerateTotpSecret();
        var qrCodeUri = GenerateQrCodeUri(user.Email, secret);
        var backupCodes = GenerateBackupCodes();

        // Store the secret temporarily (not enabled until verified)
        user.TotpSecret = secret;
        user.BackupCodes = JsonSerializer.Serialize(backupCodes.Select(HashBackupCode));

        var setupDto = new TwoFactorSetupDto
        {
            Secret = secret,
            QrCodeUri = qrCodeUri,
            BackupCodes = backupCodes
        };

        return Result<TwoFactorSetupDto>.Success(setupDto);
    }

    public async Task<Result> VerifySetupAsync(Guid userGuid, VerifyTwoFactorSetupDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result.NotFound("User not found");

        if (user.TwoFactorEnabled)
            return Result.BadRequest("Two-factor authentication is already enabled");

        if (string.IsNullOrEmpty(user.TotpSecret))
            return Result.BadRequest("Two-factor setup not initiated");

        var isValidTotp = ValidateTotpCode(user.TotpSecret, request.Code);
        if (!isValidTotp)
            return Result.BadRequest("Invalid verification code");

        // Enable 2FA
        user.TwoFactorEnabled = true;

        // Send confirmation notification
        var notification = new Notification
        {
            To = user.Email,
            Subject = "Two-Factor Authentication Enabled",
            Body = $"<p>Hello {user.FirstName},</p>" +
                   "<p>Two-factor authentication has been successfully enabled on your SplitDuo account.</p>" +
                   "<p>Your account is now more secure. You will need to provide a verification code when logging in.</p>" +
                   "<p>If you did not enable this feature, please contact support immediately.</p>"
        };

        await notificationService.EnqueueAsync(notification);

        return Result.Success();
    }

    public async Task<Result> DisableAsync(Guid userGuid, DisableTwoFactorDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result.NotFound("User not found");

        if (!user.TwoFactorEnabled)
            return Result.BadRequest("Two-factor authentication is not enabled");

        // Validate current password for security
        var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        
        if (verificationResult == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            return Result.Unauthorized("Invalid password");

        // Disable 2FA
        user.TwoFactorEnabled = false;
        user.TotpSecret = null;
        user.BackupCodes = null;

        // Revoke all active 2FA tokens
        var activeTwoFactorTokens = await unitOfWork.TwoFactorTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync();

        foreach (var token in activeTwoFactorTokens)
        {
            token.UsedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // Send notification
        var notification = new Notification
        {
            To = user.Email,
            Subject = "Two-Factor Authentication Disabled",
            Body = $"<p>Hello {user.FirstName},</p>" +
                   "<p>Two-factor authentication has been disabled on your SplitDuo account.</p>" +
                   "<p>Your account security has been reduced. Consider re-enabling 2FA for better protection.</p>" +
                   "<p>If you did not disable this feature, please contact support immediately and secure your account.</p>"
        };

        await notificationService.EnqueueAsync(notification);

        return Result.Success();
    }

    public async Task<Result<string>> GenerateEmailCodeAsync(Guid userGuid, string purpose = "2fa_login")
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<string>.NotFound("User not found");

        // Generate 6-digit code
        var code = GenerateNumericCode(6);
        var hashedCode = HashToken(code);

        // Clean up old tokens of the same purpose
        var existingTokens = await unitOfWork.TwoFactorTokens
            .Where(t => t.UserId == user.Id && t.Purpose == purpose && t.UsedAt == null)
            .ToListAsync();

        foreach (var token in existingTokens)
        {
            token.UsedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // Create new token
        var twoFactorToken = new TwoFactorToken
        {
            UserId = user.Id,
            TokenHash = hashedCode,
            TokenType = "email_verification",
            Purpose = purpose,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(), // 10 minutes
            MaxAttempts = 3,
            ClientInfo = "Email 2FA Code"
        };

        unitOfWork.TwoFactorTokens.Add(twoFactorToken);

        // Send email with code
        var notification = new Notification
        {
            To = user.Email,
            Subject = "Your SplitDuo Verification Code",
            Body = $"<p>Hello {user.FirstName},</p>" +
                   $"<p>Your verification code is: <strong>{code}</strong></p>" +
                   "<p>This code will expire in 10 minutes.</p>" +
                   "<p>If you did not request this code, please ignore this email.</p>"
        };

        await notificationService.EnqueueAsync(notification);

        return Result<string>.Success("Verification code sent to your email");
    }

    public async Task<Result<bool>> ValidateEmailCodeAsync(Guid userGuid, string code, string purpose = "2fa_login")
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<bool>.NotFound("User not found");

        var hashedCode = HashToken(code);
        var token = await unitOfWork.TwoFactorTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && 
                                     t.TokenHash == hashedCode && 
                                     t.Purpose == purpose &&
                                     t.TokenType == "email_verification");

        if (token == null)
        {
            return Result<bool>.Success(false);
        }

        // Increment attempts
        token.Attempts++;

        if (!token.IsValid)
        {
            return Result<bool>.Success(false);
        }

        // Mark as used
        token.UsedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ValidateTotpCodeAsync(Guid userGuid, string code)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<bool>.NotFound("User not found");

        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return Result<bool>.BadRequest("Two-factor authentication is not enabled");

        var isValid = ValidateTotpCode(user.TotpSecret, code);
        return Result<bool>.Success(isValid);
    }

    public async Task<Result<bool>> ValidateBackupCodeAsync(Guid userGuid, string code)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<bool>.NotFound("User not found");

        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.BackupCodes))
            return Result<bool>.BadRequest("Two-factor authentication is not enabled");

        var backupCodesJson = JsonSerializer.Deserialize<string[]>(user.BackupCodes) ?? [];
        var hashedCode = HashBackupCode(code);

        if (!backupCodesJson.Contains(hashedCode))
            return Result<bool>.Success(false);

        // Remove the used backup code
        var updatedCodes = backupCodesJson.Where(c => c != hashedCode).ToArray();
        user.BackupCodes = JsonSerializer.Serialize(updatedCodes);

        return Result<bool>.Success(true);
    }

    public async Task<Result<List<string>>> GenerateBackupCodesAsync(Guid userGuid)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<List<string>>.NotFound("User not found");

        if (!user.TwoFactorEnabled)
            return Result<List<string>>.BadRequest("Two-factor authentication is not enabled");

        var backupCodes = GenerateBackupCodes();
        user.BackupCodes = JsonSerializer.Serialize(backupCodes.Select(HashBackupCode));

        return Result<List<string>>.Success(backupCodes);
    }

    private static string GenerateTotpSecret()
    {
        var bytes = new byte[20];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return bytes.ToBase32String().Replace("=", "");
    }

    private static string GenerateQrCodeUri(string email, string secret)
    {
        var issuer = "SplitDuo";
        var accountName = $"{issuer}:{email}";
        return $"otpauth://totp/{Uri.EscapeDataString(accountName)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";
    }

    private static List<string> GenerateBackupCodes()
    {
        var codes = new List<string>();
        using var rng = RandomNumberGenerator.Create();
        
        for (int i = 0; i < 10; i++)
        {
            var codeBytes = new byte[5];
            rng.GetBytes(codeBytes);
            var code = Convert.ToHexString(codeBytes).ToLower();
            codes.Add($"{code[..4]}-{code[4..]}");
        }
        
        return codes;
    }

    private static string GenerateNumericCode(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var number = BitConverter.ToUInt32(bytes, 0);
        var code = (number % (uint)Math.Pow(10, length)).ToString($"D{length}");
        return code;
    }

    private static string HashToken(string token)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashedBytes);
    }

    private static string HashBackupCode(string code)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.ToLower()));
        return Convert.ToBase64String(hashedBytes);
    }

    private static bool ValidateTotpCode(string secret, string code)
    {
        if (code.Length != 6 || !int.TryParse(code, out _))
            return false;

        var secretBytes = Base32Extensions.FromBase32String(secret);
        var currentTimeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        // Check current time step and adjacent ones (to account for clock drift)
        for (long i = currentTimeStep - 1; i <= currentTimeStep + 1; i++)
        {
            var expectedCode = GenerateTotpCode(secretBytes, i);
            if (expectedCode == code)
                return true;
        }

        return false;
    }

    private static string GenerateTotpCode(byte[] secret, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(timeBytes);
        
        var offset = hash[^1] & 0xf;
        var binaryCode = ((hash[offset] & 0x7f) << 24) |
                         ((hash[offset + 1] & 0xff) << 16) |
                         ((hash[offset + 2] & 0xff) << 8) |
                         (hash[offset + 3] & 0xff);

        var code = binaryCode % 1000000;
        return code.ToString("D6");
    }
}

// Helper extension for Base32 encoding (needed for TOTP)
public static class Base32Extensions
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string ToBase32String(this byte[] bytes)
    {
        if (bytes.Length == 0) return string.Empty;

        var result = new StringBuilder();
        for (int i = 0; i < bytes.Length; i += 5)
        {
            var byteCount = Math.Min(5, bytes.Length - i);
            ulong buffer = 0;
            for (int j = 0; j < byteCount; j++)
            {
                buffer = (buffer << 8) | bytes[i + j];
            }

            var bitCount = byteCount * 8;
            while (bitCount > 0)
            {
                var index = (int)((buffer >> (bitCount - 5)) & 31);
                result.Append(Base32Alphabet[index]);
                bitCount -= 5;
                buffer &= (1UL << bitCount) - 1;
            }
        }

        return result.ToString();
    }

    public static byte[] FromBase32String(string base32)
    {
        if (string.IsNullOrEmpty(base32)) return [];

        base32 = base32.ToUpper().Replace("=", "");
        var result = new List<byte>();
        
        for (int i = 0; i < base32.Length; i += 8)
        {
            var blockLength = Math.Min(8, base32.Length - i);
            ulong buffer = 0;
            
            for (int j = 0; j < blockLength; j++)
            {
                var index = Base32Alphabet.IndexOf(base32[i + j]);
                if (index < 0) throw new ArgumentException("Invalid Base32 character");
                buffer = (buffer << 5) | (uint)index;
            }

            var byteCount = blockLength * 5 / 8;
            for (int j = byteCount - 1; j >= 0; j--)
            {
                result.Add((byte)((buffer >> (j * 8)) & 0xff));
            }
        }

        return result.ToArray();
    }
}