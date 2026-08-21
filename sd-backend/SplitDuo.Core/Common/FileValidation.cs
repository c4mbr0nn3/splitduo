namespace SplitDuo.Core.Common;

public static class FileValidation
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".pdf"
    };

    public static bool IsValidExtension(string filename)
    {
        var ext = Path.GetExtension(filename);
        return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
    }

    public static string? SniffMagicNumber(byte[] head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return "image/jpeg";
        if (head.Length >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47) return "image/png";
        if (head.Length >= 12 && head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46
            && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50) return "image/webp";
        if (head.Length >= 5 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46) return "application/pdf";
        // HEIC: ftyp box with heic/heix/mif1 brands (bytes 4-7 = "ftyp", bytes 8-11 = brand)
        if (head.Length >= 12 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70)
        {
            var brand = System.Text.Encoding.ASCII.GetString(head, 8, 4);
            if (brand is "heic" or "heix" or "mif1") return "image/heic";
        }
        return null;
    }

    public static string NormalizeExtension(string filename)
    {
        return Path.GetExtension(filename).ToLowerInvariant();
    }
}
