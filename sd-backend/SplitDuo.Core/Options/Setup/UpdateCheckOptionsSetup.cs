using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class UpdateCheckOptionsSetup : IConfigureOptions<UpdateCheckOptions>
{
    public void Configure(UpdateCheckOptions options)
    {
        options.Disabled = bool.TryParse(
            Environment.GetEnvironmentVariable("SD_UPDATE_CHECK_DISABLED"), out var v) && v;
    }
}