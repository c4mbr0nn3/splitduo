using System.Net.Http.Headers;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// HttpClient extensions for Expense Attachments feature test setup.
/// </summary>
public static class AttachmentTestExtensions
{
    // Minimal valid file headers — the service's magic-number sniff only checks leading bytes
    public static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    public static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0xE2, 0xE3, 0xCF, 0xD3];
    public static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    public static readonly byte[] WebpBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
    public static readonly byte[] HeicBytes = [0x00, 0x00, 0x00, 0x00, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63];
    // DOS executable header (MZ) — passes the extension check but fails the magic-number sniff
    public static readonly byte[] BadMagicBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    /// <summary>
    /// Returns a valid-JPEG byte array with a unique trailing byte (unique hash) for tests
    /// that need multiple distinct files.
    /// </summary>
    public static byte[] DistinctJpegBytes(int index) => [.. JpegBytes, (byte)(index % 256)];

    /// <summary>
    /// Uploads a file to POST /api/v1/groups/{groupId}/expenses/{expenseId}/attachments
    /// and returns the raw response.
    /// </summary>
    public static async Task<HttpResponseMessage> UploadAttachmentAsync(
        this HttpClient client,
        string groupId,
        string expenseId,
        byte[] bytes,
        string fileName,
        string? contentType = null)
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        content.Add(fileContent, "file", fileName);

        return await client.PostAsync(
            $"/api/v1/groups/{groupId}/expenses/{expenseId}/attachments", content, ct);
    }
}
