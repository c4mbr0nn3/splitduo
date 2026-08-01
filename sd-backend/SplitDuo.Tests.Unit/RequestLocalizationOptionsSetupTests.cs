using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using SplitDuo.Core.Options.Setup;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class RequestLocalizationOptionsSetupTests
{
    private static RequestLocalizationOptionsSetup CreateSetup() => new();

    #region Culture configuration

    [Fact]
    public void Configure_SetsSupportedCultures_EnAndIt()
    {
        var setup = CreateSetup();
        var options = new RequestLocalizationOptions();

        setup.Configure(options);

        Assert.NotNull(options.SupportedCultures);
        Assert.Contains(options.SupportedCultures!, c => c.Name == "en");
        Assert.Contains(options.SupportedCultures!, c => c.Name == "it");
        Assert.Equal(2, options.SupportedCultures!.Count);
    }

    [Fact]
    public void Configure_SetsSupportedUICultures_EnAndIt()
    {
        var setup = CreateSetup();
        var options = new RequestLocalizationOptions();

        setup.Configure(options);

        Assert.NotNull(options.SupportedUICultures);
        Assert.Contains(options.SupportedUICultures!, c => c.Name == "en");
        Assert.Contains(options.SupportedUICultures!, c => c.Name == "it");
        Assert.Equal(2, options.SupportedUICultures!.Count);
    }

    [Fact]
    public void Configure_SetsDefaultRequestCulture_En()
    {
        var setup = CreateSetup();
        var options = new RequestLocalizationOptions();

        setup.Configure(options);

        Assert.Equal("en", options.DefaultRequestCulture.Culture.Name);
    }

    #endregion

    #region Custom culture provider

    [Fact]
    public void Configure_AddsCustomRequestCultureProvider()
    {
        var setup = CreateSetup();
        var options = new RequestLocalizationOptions();

        setup.Configure(options);

        Assert.NotEmpty(options.RequestCultureProviders);
        Assert.Contains(options.RequestCultureProviders, p => p is CustomRequestCultureProvider);
        Assert.IsType<CustomRequestCultureProvider>(options.RequestCultureProviders[0]);
    }

    #endregion
}
