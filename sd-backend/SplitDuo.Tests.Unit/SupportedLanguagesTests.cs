using System.Globalization;
using SplitDuo.Core.Localization;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class SupportedLanguagesTests
{
    #region Default

    [Fact]
    public void Default_ReturnsEn()
    {
        Assert.Equal("en", SupportedLanguages.Default);
    }

    #endregion

    #region All

    [Fact]
    public void All_ContainsEnAndIt()
    {
        Assert.Equal(2, SupportedLanguages.All.Count);
        Assert.Contains("en", SupportedLanguages.All);
        Assert.Contains("it", SupportedLanguages.All);
    }

    [Fact]
    public void All_IsReadOnly()
    {
        Assert.IsAssignableFrom<IReadOnlySet<string>>(SupportedLanguages.All);
    }

    #endregion

    #region IsSupported

    [Fact]
    public void IsSupported_En_ReturnsTrue()
    {
        Assert.True(SupportedLanguages.IsSupported("en"));
    }

    [Fact]
    public void IsSupported_It_ReturnsTrue()
    {
        Assert.True(SupportedLanguages.IsSupported("it"));
    }

    [Fact]
    public void IsSupported_Fr_ReturnsFalse()
    {
        Assert.False(SupportedLanguages.IsSupported("fr"));
    }

    [Fact]
    public void IsSupported_Null_ReturnsFalse()
    {
        Assert.False(SupportedLanguages.IsSupported(null));
    }

    [Fact]
    public void IsSupported_EmptyString_ReturnsFalse()
    {
        Assert.False(SupportedLanguages.IsSupported(""));
    }

    #endregion

    #region Normalize

    [Fact]
    public void Normalize_En_ReturnsEn()
    {
        Assert.Equal("en", SupportedLanguages.Normalize("en"));
    }

    [Fact]
    public void Normalize_It_ReturnsIt()
    {
        Assert.Equal("it", SupportedLanguages.Normalize("it"));
    }

    [Fact]
    public void Normalize_Fr_ReturnsEn()
    {
        Assert.Equal("en", SupportedLanguages.Normalize("fr"));
    }

    [Fact]
    public void Normalize_Null_ReturnsEn()
    {
        Assert.Equal("en", SupportedLanguages.Normalize(null));
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsEn()
    {
        Assert.Equal("en", SupportedLanguages.Normalize(""));
    }

    #endregion

    #region Cultures

    [Fact]
    public void Cultures_ContainsEnAndIt()
    {
        Assert.Equal(2, SupportedLanguages.Cultures.Length);
        Assert.Contains(SupportedLanguages.Cultures, c => c.Name == "en");
        Assert.Contains(SupportedLanguages.Cultures, c => c.Name == "it");
    }

    [Fact]
    public void Cultures_AreCultureInfoInstances()
    {
        foreach (var culture in SupportedLanguages.Cultures)
        {
            Assert.IsType<CultureInfo>(culture);
        }
    }

    #endregion
}
