namespace SplitDuo.Core.Options;

public class DatabaseOptions
{
    public const string SectionName = "Database";
    public required string Host { get; set; }
    public required string Port { get; set; }
    public required string Database { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }

    public string ConnectionString =>
        $"Host={Host};" +
        $"Port={Port};" +
        $"Database={Database};" +
        $"Username={Username};" +
        $"Password={Password};";
}