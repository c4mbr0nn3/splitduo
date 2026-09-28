using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Ai.Services;

public sealed record AiCallOutcome(
    string Feature,
    string Model,
    int UserId,
    long RequestedAt,
    long? RespondedAt,
    int LatencyMs,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens,
    bool Success,
    string? ErrorMessage);

public interface IAiCallLogger
{
    Task LogAsync(AiCallOutcome outcome, CancellationToken ct = default);
}

public class AiCallLogger(IUnitOfWork unitOfWork) : IAiCallLogger
{
    public async Task LogAsync(AiCallOutcome outcome, CancellationToken ct = default)
    {
        // Intentional exception to the "controller saves" convention: this is a
        // fire-and-forget audit log written regardless of caller outcome.
        unitOfWork.AiCallLogs.Add(new AiCallLog
        {
            UserId = outcome.UserId,
            RequestedAt = outcome.RequestedAt,
            RespondedAt = outcome.RespondedAt,
            LatencyMs = outcome.LatencyMs,
            InputTokens = outcome.InputTokens,
            OutputTokens = outcome.OutputTokens,
            TotalTokens = outcome.TotalTokens,
            Model = outcome.Model,
            Feature = outcome.Feature,
            Success = outcome.Success,
            ErrorMessage = outcome.ErrorMessage,
        });
        await unitOfWork.SaveChangesAsync(ct);
    }
}