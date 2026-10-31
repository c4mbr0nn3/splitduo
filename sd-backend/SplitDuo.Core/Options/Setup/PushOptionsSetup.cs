using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class PushOptionsSetup : IConfigureOptions<PushOptions>
{
    public void Configure(PushOptions options)
    {
        options.VapidPublicKey = Environment.GetEnvironmentVariable("SD_VAPID_PUBLIC_KEY") ?? string.Empty;
        options.VapidPrivateKey = Environment.GetEnvironmentVariable("SD_VAPID_PRIVATE_KEY") ?? string.Empty;
        options.Subject = Environment.GetEnvironmentVariable("SD_VAPID_SUBJECT") ?? PushOptions.DefaultSubject;
    }
}