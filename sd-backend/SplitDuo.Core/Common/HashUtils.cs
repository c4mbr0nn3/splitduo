using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace SplitDuo.Core.Common;

public static class HashUtils
{
    public static async Task<string> ComputeSha256Async(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var sha256 = SHA256.Create();

        var hashBytes = await sha256.ComputeHashAsync(stream);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string ComputeSha256(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static async Task<string> ComputeSha256Async(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();

        var hashBytes = await sha256.ComputeHashAsync(stream);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string Sha256Base64(string input)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashedBytes);
    }
}