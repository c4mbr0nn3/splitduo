using System.Net.Http.Headers;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// HttpClient extensions for User Avatars feature test setup.
/// </summary>
public static class AvatarTestExtensions
{
    // Minimal valid file headers — the service's magic-number sniff only checks leading bytes
    public static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    public static readonly byte[] WebpBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
    // HEIC: ftyp box with heic brand — valid magic number, rejected by the post-sniff MIME filter
    public static readonly byte[] HeicBytes = [0x00, 0x00, 0x00, 0x00, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63];
    // DOS executable header (MZ) — passes the extension check but fails the magic-number sniff
    public static readonly byte[] BadMagicBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    /// <summary>
    /// Returns a valid-JPEG byte array with a unique trailing byte (unique hash) for tests
    /// that need multiple distinct files.
    /// </summary>
    public static byte[] DistinctJpegBytes(int index) => [.. JpegBytes, (byte)(index % 256)];

    /// <summary>
    /// Uploads a file to PUT /api/v1/users/me/avatar and returns the raw response.
    /// </summary>
    public static async Task<HttpResponseMessage> UploadAvatarAsync(
        this HttpClient client,
        byte[] bytes,
        string fileName,
        string? contentType = null)
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        content.Add(fileContent, "file", fileName);

        return await client.PutAsync("/api/v1/users/me/avatar", content, ct);
    }
}
