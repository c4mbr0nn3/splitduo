namespace SplitDuo.Core.Options;

public class SmtpOptions
{
    public const string SectionName = "Smtp";
    public required string SenderName { get; set; }
    public required string SenderAddress { get; set; }
    public required string SmtpServer { get; set; }
    public required int SmtpPort { get; set; }
    public required string SmtpUsername { get; set; }
    public required string SmtpPassword { get; set; }
    public bool UseSsl { get; set; }
}