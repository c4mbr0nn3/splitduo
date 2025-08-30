using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class DatabaseOptionsSetup : IConfigureOptions<DatabaseOptions>
{
    public void Configure(DatabaseOptions options)
    {
        options.Host = Environment.GetEnvironmentVariable("PB_DB_HOST") ?? "localhost";
        options.Port = Environment.GetEnvironmentVariable("PB_DB_PORT") ?? "5432";
        options.Database = Environment.GetEnvironmentVariable("PB_DB_NAME") ?? "pocket-barber";
        options.Username = Environment.GetEnvironmentVariable("PB_DB_USERNAME") ?? "pocket-barber";
        options.Password = Environment.GetEnvironmentVariable("PB_DB_PASSWORD") ?? "pocket-barber";
    }
}
