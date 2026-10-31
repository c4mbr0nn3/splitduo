using System.Buffers.Text;

namespace SplitDuo.Core.Options;

/// <summary>
/// Shape check for a VAPID public key, deliberately not a cryptographic validation:
/// it catches the real-world failure (typo, truncation, wrong key type, PEM pasted by
/// mistake) without needing a P-256 implementation here.
///
/// A VAPID public key is the uncompressed P-256 point: 65 bytes starting with 0x04,
/// base64url-encoded without padding (RFC 8292 §3.2). Standard base64 and padding are
/// accepted too, because hand-generated keys are often pasted that way.
/// </summary>
internal static class VapidKeyFormat
{
    private const int UncompressedPointLength = 65;
    private const byte UncompressedPointPrefix = 0x04;

    internal static bool LooksLikePublicKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var normalized = key.Trim().Replace('-', '+').Replace('_', '/');

        // Convert.TryFromBase64String rejects non-multiple-of-4 input, so pad it first:
        // operators paste both padded and unpadded forms.
        var padding = (4 - normalized.Length % 4) % 4;
        normalized = normalized.PadRight(normalized.Length + padding, '=');

        var buffer = new byte[UncompressedPointLength + 4];
        if (!Convert.TryFromBase64String(normalized, buffer, out var written))
            return false;

        return written == UncompressedPointLength && buffer[0] == UncompressedPointPrefix;
    }
}
