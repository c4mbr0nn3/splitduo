using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class AppOptionsSetup : IConfigureOptions<AppOptions>
{
    public void Configure(AppOptions options)
    {
        options.Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        options.BaseUrl = Environment.GetEnvironmentVariable("SD_BASE_URL") ?? "http://localhost:3000";
        options.InitialUserEmail = Environment.GetEnvironmentVariable("SD_INITIAL_USER_EMAIL") ?? "admin@splitduo.local";
        options.InitialUserFirstName = Environment.GetEnvironmentVariable("SD_INITIAL_USER_FIRSTNAME") ?? "Super";
        options.InitialUserLastName = Environment.GetEnvironmentVariable("SD_INITIAL_USER_LASTNAME") ?? "Admin";
        options.InitialUserPassword = Environment.GetEnvironmentVariable("SD_INITIAL_USER_PASSWORD") ?? "changeme123";
        options.SeedDemoData = bool.TryParse(
            Environment.GetEnvironmentVariable("SD_SEED_DEMO_DATA"), out var v) && v;
    }
}