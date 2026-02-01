using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class DatabaseOptionsSetup : IConfigureOptions<DatabaseOptions>
{
    public void Configure(DatabaseOptions options)
    {
        options.Host = Environment.GetEnvironmentVariable("SD_DB_HOST") ?? "localhost";
        options.Port = Environment.GetEnvironmentVariable("SD_DB_PORT") ?? "5432";
        options.Database = Environment.GetEnvironmentVariable("SD_DB_NAME") ?? "splitduo";
        options.Username = Environment.GetEnvironmentVariable("SD_DB_USERNAME") ?? "splitduo";
        options.Password = Environment.GetEnvironmentVariable("SD_DB_PASSWORD") ?? "splitduo";
    }
}