using Microsoft.Extensions.Options;

namespace SplitDuo.Core.Options.Setup;

public class SmtpOptionsSetup : IConfigureOptions<SmtpOptions>
{
    public void Configure(SmtpOptions options)
    {
        options.SenderName = Environment.GetEnvironmentVariable("SD_EMAIL_SENDER_NAME") ?? "SplitDuo";
        options.SenderAddress =
            Environment.GetEnvironmentVariable("SD_EMAIL_SENDER_ADDRESS") ?? "noreply@splitduo.app";
        options.SmtpServer = Environment.GetEnvironmentVariable("SD_EMAIL_SMTP_HOST") ?? "localhost";
        options.SmtpPort = int.TryParse(Environment.GetEnvironmentVariable("SD_EMAIL_SMTP_PORT"), out var port)
            ? port
            : 1025;
        options.SmtpUsername = Environment.GetEnvironmentVariable("SD_EMAIL_SMTP_USERNAME") ?? "any";
        options.SmtpPassword = Environment.GetEnvironmentVariable("SD_EMAIL_SMTP_PASSWORD") ?? "";
        options.UseSsl = bool.Parse(Environment.GetEnvironmentVariable("SD_EMAIL_SSL") ?? "false");
    }
}