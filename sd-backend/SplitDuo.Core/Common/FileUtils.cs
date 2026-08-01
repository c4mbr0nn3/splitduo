using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace SplitDuo.Core.Common;

public static class FileUtils
{
    public static Result CheckExtensionAndSize(IFormFile file, IStringLocalizer? loc = null)
    {
        if (file.Length == 0)
        {
            return Result.BadRequest(loc?["FileEmpty"] ?? "File is empty");
        }

        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv")
        {
            return Result.BadRequest(loc?["OnlyCsvAllowed"] ?? "Only CSV files are allowed");
        }

        // Check file size (e.g., max 10MB)
        const long maxFileSize = 10 * 1024 * 1024; // 10MB
        return file.Length > maxFileSize
            ? Result.BadRequest(loc?["FileSizeExceeded"] ?? "File size exceeds the maximum allowed size of 10MB")
            : Result.Success();
    }

    public static async Task<string> SaveTempFileAsync(IFormFile file, Guid importGuid)
    {
        var tempDir = Path.GetTempPath();
        var fileName = GetTempFileName(importGuid);
        var tempFilePath = Path.Combine(tempDir, fileName);

        await using var stream = new FileStream(tempFilePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return tempFilePath;
    }

    public static async Task<byte[]> ConvertToByteArrayAsync(IFormFile file)
    {
        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    public static void CleanupTempFile(Guid importGuid)
    {
        var tempDir = Path.GetTempPath();
        var fileName = GetTempFileName(importGuid);
        var tempFilePath = Path.Combine(tempDir, fileName);
        if (string.IsNullOrEmpty(tempFilePath) || !File.Exists(tempFilePath)) return;

        File.Delete(tempFilePath);
    }

    public static string GetTempFileName(Guid importGuid) =>
        $"import_{importGuid}";
}