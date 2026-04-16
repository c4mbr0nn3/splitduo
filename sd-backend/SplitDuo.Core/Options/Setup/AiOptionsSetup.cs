using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class AiOptionsSetup(IConfiguration configuration, ILogger<AiOptionsSetup> logger) : IConfigureOptions<AiOptions>
{
    public void Configure(AiOptions options)
    {
        var opt = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();

        options.BaseUrl = Environment.GetEnvironmentVariable("SD_AI_BASE_URL") ?? opt.BaseUrl;
        options.ApiKey = Environment.GetEnvironmentVariable("SD_AI_API_KEY") ?? opt.ApiKey;
        options.Model = Environment.GetEnvironmentVariable("SD_AI_MODEL") ?? opt.Model;

        if (options.IsEnabled)
            logger.LogInformation("AI module enabled. BaseUrl={BaseUrl}, Model={Model}", options.BaseUrl,
                options.Model);
        else
            logger.LogInformation("AI module disabled. Set SD_AI_BASE_URL and SD_AI_MODEL to enable.");
    }
}