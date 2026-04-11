using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Quartz;
using SplitDuo.Core.Options;

namespace SplitDuo.Core.Services.BackgroundJobs;

[DisallowConcurrentExecution]
public class LogCleanupJob(ILogger<LogCleanupJob> logger, IOptions<DatabaseOptions> dbOptions) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await using var connection = new NpgsqlConnection(dbOptions.Value.ConnectionString);
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