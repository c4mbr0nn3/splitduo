using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace SplitDuo.Core.Common;

public static class HashUtils
{
    /// <summary>
    /// Calculates a hash of the file content using MD5 algorithm.
    /// This is suitable for detecting duplicate files without security requirements.
    /// </summary>
    public static async Task<string> CalculateFileHashAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var md5 = MD5.Create();

        var hashBytes = await md5.ComputeHashAsync(stream);

        // Reset stream position for subsequent use
        stream.Position = 0;

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates a hash of file content from a file path using MD5 algorithm.
    /// </summary>
    public static async Task<string> CalculateFileHashAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var md5 = MD5.Create();

        var hashBytes = await md5.ComputeHashAsync(stream);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}