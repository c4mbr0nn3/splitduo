using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;

namespace SplitDuo.Core.Services.BackgroundJobs;

[DisallowConcurrentExecution]
public class LogCleanupJob(ILogger<LogCleanupJob> logger, IConfiguration configuration) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                logger.LogWarning("Database connection string not found, skipping log cleanup");
                return;
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(context.CancellationToken);

            await using var command = new NpgsqlCommand(
                "DELETE FROM logging.logs WHERE \"Timestamp\" < NOW() - INTERVAL '30 days'",
                connection);

            var deletedRows = await command.ExecuteNonQueryAsync(context.CancellationToken);

            logger.LogInformation("Log cleanup completed: {DeletedRows} log entries removed", deletedRows);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup old log entries");
            throw;
        }
    }
}