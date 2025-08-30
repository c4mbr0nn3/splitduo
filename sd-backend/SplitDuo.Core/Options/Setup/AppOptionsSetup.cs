using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class AppOptionsSetup : IConfigureOptions<AppOptions>
{
    public void Configure(AppOptions options)
    {
        options.Environment = Environment.GetEnvironmentVariable("SD_ENVIRONMENT") ?? "Development";
        options.BaseUrl = Environment.GetEnvironmentVariable("SD_BASE_URL") ?? "http://localhost:3000";
    }
}