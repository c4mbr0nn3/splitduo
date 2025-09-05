using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class AppOptionsSetup : IConfigureOptions<AppOptions>
{
    public void Configure(AppOptions options)
    {
        options.Environment = Environment.GetEnvironmentVariable("SD_ENVIRONMENT") ?? "Development";
        options.BaseUrl = Environment.GetEnvironmentVariable("SD_BASE_URL") ?? "http://localhost:3000";
        options.InitialUserEmail = Environment.GetEnvironmentVariable("SD_INITIAL_USER_EMAIL") ?? "admin@localhost";
        options.InitialUserFirstName = Environment.GetEnvironmentVariable("SD_INITIAL_USER_FIRSTNAME") ?? "Super";
        options.InitialUserLastName = Environment.GetEnvironmentVariable("SD_INITIAL_USER_LASTNAME") ?? "Admin";
        options.InitialUserPassword = Environment.GetEnvironmentVariable("SD_INITIAL_USER_PASSWORD") ?? "changeme";
    }
}