using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpNet;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;
using SplitDuo.Api.Features.Authentication.Dto;

namespace SplitDuo.Api.Features.Authentication.Services;

public interface ITwoFactorService
{
    Task<Result<TwoFactorSetupDto>> InitiateSetupAsync(Guid userGuid);
    Task<Result> VerifySetupAsync(Guid userGuid, VerifyTwoFactorSetupDto request);
    Task<Result> DisableAsync(Guid userGuid, DisableTwoFactorDto request);
    Task<Result<bool>> ValidateTotpCodeAsync(Guid userGuid, string code);
    Task<Result<bool>> ValidateBackupCodeAsync(Guid userGuid, string code);
    Task<Result<List<string>>> GenerateBackupCodesAsync(Guid userGuid);
}

public class TwoFactorService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    IEmailTemplateProvider emailTemplateProvider,
    IOptions<JwtOptions> jwtOptions,
    IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider) : ITwoFactorService
{
    private readonly byte[] _encryptionKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("totp:" + jwtOptions.Value.SecretKey));

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

        // Store the secret encrypted (not enabled until verified)
        user.TotpSecret = EncryptTotpSecret(secret, _encryptionKey);
        user.BackupCodes = JsonSerializer.Serialize(backupCodes.Select(c => HashUtils.Sha256Base64(c.ToLower())));

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

        var plainSecret = DecryptTotpSecret(user.TotpSecret, _encryptionKey);
        var totp = new Totp(Base32Encoding.ToBytes(plainSecret));
        if (!totp.VerifyTotp(request.Code, out _, new VerificationWindow(1, 1)))
            return Result.BadRequest("Invalid verification code");

        // Enable 2FA
        user.TwoFactorEnabled = true;

        // Send confirmation notification
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new TwoFactorEnabledModel
            { To = user.Email, FirstName = user.FirstName }));

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
        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
            return Result.Unauthorized("Invalid password");

        // Disable 2FA
        user.TwoFactorEnabled = false;
        user.TotpSecret = null;
        user.BackupCodes = null;
        user.LastUsedTotpTimeStep = null;

        // Revoke all active 2FA tokens
        var activeTwoFactorTokens = await unitOfWork.TwoFactorTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync();

        foreach (var token in activeTwoFactorTokens)
        {
            token.UsedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        }

        // Send notification
        await notificationService.EnqueueAsync(emailTemplateProvider.Render(new TwoFactorDisabledModel
            { To = user.Email, FirstName = user.FirstName }));

        return Result.Success();
    }

    public async Task<Result<bool>> ValidateTotpCodeAsync(Guid userGuid, string code)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (user == null)
            return Result<bool>.NotFound("User not found");

        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return Result<bool>.BadRequest("Two-factor authentication is not enabled");

        var plainSecret = DecryptTotpSecret(user.TotpSecret, _encryptionKey);
        var totp = new Totp(Base32Encoding.ToBytes(plainSecret));
        var isValid = totp.VerifyTotp(code, out var usedTimeStep, new VerificationWindow(1, 1));

        if (!isValid) return Result<bool>.Success(false);

        if (user.LastUsedTotpTimeStep.HasValue && usedTimeStep <= user.LastUsedTotpTimeStep.Value)
            return Result<bool>.Success(false); // replay rejected

        user.LastUsedTotpTimeStep = usedTimeStep;
        return Result<bool>.Success(true);
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
        var hashedCode = HashUtils.Sha256Base64(code.ToLower());

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
        user.BackupCodes = JsonSerializer.Serialize(backupCodes.Select(c => HashUtils.Sha256Base64(c.ToLower())));

        return Result<List<string>>.Success(backupCodes);
    }

    private static string GenerateTotpSecret()
    {
        var bytes = new byte[20];
        RandomNumberGenerator.Fill(bytes);
        return Base32Encoding.ToString(bytes).TrimEnd('=');
    }

    private static string GenerateQrCodeUri(string email, string secret)
    {
        var issuer = "SplitDuo";
        var accountName = $"{issuer}:{email}";
        return
            $"otpauth://totp/{Uri.EscapeDataString(accountName)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";
    }

    private static List<string> GenerateBackupCodes()
    {
        var codes = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var codeBytes = new byte[5];
            RandomNumberGenerator.Fill(codeBytes);
            var code = Convert.ToHexString(codeBytes).ToLower();
            codes.Add($"{code[..4]}-{code[4..]}");
        }

        return codes;
    }

    /// <summary>
    /// Encrypts a TOTP secret using AES-256-GCM authenticated encryption.
    ///
    /// AES-GCM produces three outputs: ciphertext (the encrypted data), a nonce (random
    /// value that must be unique per encryption), and an authentication tag (a checksum
    /// that proves the ciphertext hasn't been tampered with). All three are needed to
    /// decrypt, so they are concatenated and stored together as a single Base64 string:
    ///
    ///   [ nonce (12 bytes) | ciphertext (variable) | tag (16 bytes) ]
    ///
    /// The nonce does not need to be secret — it just must never be reused with the same
    /// key. Generating it randomly is safe here because TOTP secrets are written once per
    /// enrollment, so the collision probability is negligible.
    /// </summary>
    private static string EncryptTotpSecret(string secret, byte[] key)
    {
        // GCM requires a 96-bit (12-byte) nonce, unique per (key, message) pair.
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var plaintext = Encoding.UTF8.GetBytes(secret);

        // GCM is a stream cipher: ciphertext is always the same length as plaintext (no padding).
        var ciphertext = new byte[plaintext.Length];

        // The authentication tag proves integrity on decryption; 16 bytes is the maximum and recommended size.
        var tag = new byte[16];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Pack all three pieces into one buffer so they can be stored as a single value.
        var result = new byte[12 + ciphertext.Length + 16];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, 12);
        tag.CopyTo(result, 12 + ciphertext.Length);

        // Base64 encodes the binary blob to a text string safe for database storage.
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a TOTP secret that was encrypted by <see cref="EncryptTotpSecret"/>.
    ///
    /// The stored blob is [ nonce (12 bytes) | ciphertext (variable) | tag (16 bytes) ].
    /// Because nonce and tag are fixed sizes, their positions can be sliced out with
    /// constants. AesGcm.Decrypt verifies the authentication tag before returning
    /// plaintext — if the ciphertext or tag was altered it throws <see cref="CryptographicException"/>.
    /// </summary>
    private static string DecryptTotpSecret(string encrypted, byte[] key)
    {
        // Decode the Base64 string back to the raw [ nonce | ciphertext | tag ] bytes.
        var data = Convert.FromBase64String(encrypted);

        // Plaintext is everything except the 12-byte nonce and 16-byte tag (= 28 bytes overhead).
        var plaintext = new byte[data.Length - 28];

        using var aes = new AesGcm(key, 16);

        // Slice the blob back into its three components using the known fixed offsets.
        // data[..12]   = nonce       (first 12 bytes)
        // data[12..^16] = ciphertext (middle, variable length)
        // data[^16..]  = tag         (last 16 bytes)
        aes.Decrypt(data[..12], data[12..^16], data[^16..], plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}