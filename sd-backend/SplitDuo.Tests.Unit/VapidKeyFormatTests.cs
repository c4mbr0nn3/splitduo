using SplitDuo.Core.Options;
using Xunit;

namespace SplitDuo.Tests.Unit;

/// <summary>
/// Guards the VAPID shape check that decides whether push is offered at all. If it were
/// too strict, a valid deployment silently loses notifications; too loose, and a typo'd
/// key enables a toggle whose every send fails.
/// </summary>
public class VapidKeyFormatTests
{
    /// <summary>A 65-byte uncompressed P-256 point (0x04 prefix), base64url without padding.</summary>
    private const string ValidKey =
        "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A";

    private static byte[] ValidKeyBytes() =>
        [0x04, .. Enumerable.Range(1, 64).Select(i => (byte)i)];

    [Fact]
    public void LooksLikePublicKey_AcceptsBase64UrlWithoutPadding()
    {
        Assert.True(VapidKeyFormat.LooksLikePublicKey(ValidKey));
    }

    [Fact]
    public void LooksLikePublicKey_AcceptsBase64WithPadding()
    {
        Assert.True(VapidKeyFormat.LooksLikePublicKey(Convert.ToBase64String(ValidKeyBytes())));
    }

    [Fact]
    public void LooksLikePublicKey_AcceptsStandardBase64AlphabetAndTrimsWhitespace()
    {
        // Bytes chosen so the standard encoding uses '+' and '/', which a paste may contain.
        var bytes = new byte[65];
        bytes[0] = 0x04;
        for (var i = 1; i < bytes.Length; i++)
            bytes[i] = 0xFB;

        var encoded = Convert.ToBase64String(bytes);
        Assert.Contains('+', encoded);
        Assert.True(VapidKeyFormat.LooksLikePublicKey($"  {encoded}\n"));
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("   ")] // whitespace only
    [InlineData("not-a-key")]
    [InlineData("BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0")] // 64 bytes
    [InlineData("-----BEGIN PUBLIC KEY-----\nBAECAwQF\n-----END PUBLIC KEY-----")] // PEM pasted by mistake
    public void LooksLikePublicKey_RejectsMalformedInput(string key)
    {
        Assert.False(VapidKeyFormat.LooksLikePublicKey(key));
    }

    [Fact]
    public void LooksLikePublicKey_RejectsNull()
    {
        Assert.False(VapidKeyFormat.LooksLikePublicKey(null));
    }

    [Fact]
    public void LooksLikePublicKey_RejectsCompressedPointPrefix()
    {
        // 33-byte compressed points (0x02/0x03) and a 65-byte blob starting 0x02 are not
        // what the browser expects in applicationServerKey.
        var compressed = new byte[65];
        compressed[0] = 0x02;
        for (var i = 1; i < compressed.Length; i++)
            compressed[i] = 0x11;

        Assert.False(VapidKeyFormat.LooksLikePublicKey(Convert.ToBase64String(compressed)));
    }

    [Fact]
    public void LooksLikePublicKey_RejectsValidShapedKeyOfWrongLength()
    {
        var tooLong = new byte[66];
        tooLong[0] = 0x04;

        Assert.False(VapidKeyFormat.LooksLikePublicKey(Convert.ToBase64String(tooLong)));
    }

    [Fact]
    public void IsConfigured_RequiresBothKeys()
    {
        Assert.False(new PushOptions { VapidPublicKey = ValidKey }.IsConfigured);
        Assert.False(new PushOptions { VapidPrivateKey = "private" }.IsConfigured);
        Assert.True(new PushOptions { VapidPublicKey = ValidKey, VapidPrivateKey = "private" }.IsConfigured);
    }

    [Fact]
    public void IsConfigured_TreatsMalformedPublicKeyAsDisabled()
    {
        var options = new PushOptions { VapidPublicKey = "BPublicKeyForTests", VapidPrivateKey = "private" };

        Assert.True(options.HasAnyKey);
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void HasAnyKey_DistinguishesOffFromMisconfigured()
    {
        Assert.False(new PushOptions().HasAnyKey);
        Assert.True(new PushOptions { VapidPrivateKey = "private" }.HasAnyKey);
    }
}
