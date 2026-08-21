using SplitDuo.Core.Common;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class FileValidationTests
{
    #region IsValidExtension

    [Theory]
    [InlineData("receipt.jpg")]
    [InlineData("receipt.jpeg")]
    [InlineData("receipt.png")]
    [InlineData("receipt.webp")]
    [InlineData("receipt.heic")]
    [InlineData("receipt.heif")]
    [InlineData("receipt.pdf")]
    public void IsValidExtension_AllowedExtensions_ReturnsTrue(string filename)
    {
        Assert.True(FileValidation.IsValidExtension(filename));
    }

    [Theory]
    [InlineData("receipt.JPG")]
    [InlineData("receipt.JPEG")]
    [InlineData("receipt.PNG")]
    [InlineData("receipt.WEBP")]
    [InlineData("receipt.HEIC")]
    [InlineData("receipt.HEIF")]
    [InlineData("receipt.PDF")]
    public void IsValidExtension_AllowedExtensionsUppercase_ReturnsTrue(string filename)
    {
        Assert.True(FileValidation.IsValidExtension(filename));
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("animation.gif")]
    [InlineData("notes.txt")]
    [InlineData("noextension")]
    [InlineData("")]
    public void IsValidExtension_DisallowedOrMissing_ReturnsFalse(string filename)
    {
        Assert.False(FileValidation.IsValidExtension(filename));
    }

    [Fact]
    public void IsValidExtension_Null_ReturnsFalse()
    {
        Assert.False(FileValidation.IsValidExtension(null!));
    }

    #endregion

    #region SniffMagicNumber

    [Fact]
    public void SniffMagicNumber_Jpeg_ReturnsImageJpeg()
    {
        byte[] head = [0xFF, 0xD8, 0xFF, 0xE0];
        Assert.Equal("image/jpeg", FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_Png_ReturnsImagePng()
    {
        byte[] head = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal("image/png", FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_Webp_ReturnsImageWebp()
    {
        byte[] head = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
        Assert.Equal("image/webp", FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_Pdf_ReturnsApplicationPdf()
    {
        byte[] head = [0x25, 0x50, 0x44, 0x46, 0x2D];
        Assert.Equal("application/pdf", FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_Heic_ReturnsImageHeic()
    {
        byte[] head = [0x00, 0x00, 0x00, 0x00, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63];
        Assert.Equal("image/heic", FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_HeicHeixVariant_ReturnsImageHeic()
    {
        byte[] head = [0x00, 0x00, 0x00, 0x00, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x78];
        Assert.Equal("image/heic", FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_HeicMif1Variant_ReturnsImageHeic()
    {
        byte[] head = [0x00, 0x00, 0x00, 0x00, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x69, 0x66, 0x31];
        Assert.Equal("image/heic", FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_RandomBytes_ReturnsNull()
    {
        byte[] head = [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0];
        Assert.Null(FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_ExeRenamedAsJpg_ReturnsNull()
    {
        // DOS executable header (MZ) — not a real JPEG despite the .jpg extension
        byte[] head = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
        Assert.Null(FileValidation.SniffMagicNumber(head));
    }

    [Fact]
    public void SniffMagicNumber_EmptyArray_ReturnsNull()
    {
        Assert.Null(FileValidation.SniffMagicNumber([]));
    }

    [Fact]
    public void SniffMagicNumber_TooShortArray_ReturnsNull()
    {
        // Only 2 bytes — not enough for any signature
        byte[] head = [0xFF, 0xD8];
        Assert.Null(FileValidation.SniffMagicNumber(head));
    }

    #endregion

    #region NormalizeExtension

    [Theory]
    [InlineData("receipt.JPG", ".jpg")]
    [InlineData("receipt.PNG", ".png")]
    [InlineData("receipt.PDF", ".pdf")]
    [InlineData("receipt.jpg", ".jpg")]
    [InlineData("noextension", "")]
    public void NormalizeExtension_ReturnsLowercaseExtension(string filename, string expected)
    {
        Assert.Equal(expected, FileValidation.NormalizeExtension(filename));
    }

    #endregion
}
