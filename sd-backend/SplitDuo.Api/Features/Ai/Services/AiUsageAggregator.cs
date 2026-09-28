using System.Globalization;
using SplitDuo.Api.Features.Ai.Dto;

namespace SplitDuo.Api.Features.Ai.Services;

public sealed record AiUsageRowProjection(
    long RequestedAt, int? InputTokens, int? OutputTokens, int? TotalTokens,
    int? LatencyMs, bool Success, string Model, Guid UserGuid, string? UserEmail);

public static class AiUsageAggregator
{
    private const long SecondsPerDay = 86400;

    public static AiUsageSummaryDto Aggregate(IReadOnlyList<AiUsageRowProjection> rows)
    {
        var summary = new AiUsageSummaryDto
        {
            TotalCalls = rows.Count,
            SuccessfulCalls = rows.Count(r => r.Success),
            TotalInputTokens = rows.Sum(r => (long)(r.InputTokens ?? 0)),
            TotalOutputTokens = rows.Sum(r => (long)(r.OutputTokens ?? 0)),
            TotalTokens = rows.Sum(r => (long)(r.TotalTokens ?? 0)),
        };
        summary.FailedCalls = summary.TotalCalls - summary.SuccessfulCalls;
        summary.SuccessRate = summary.TotalCalls == 0 ? 0.0 : (double)summary.SuccessfulCalls / summary.TotalCalls;

        var latencies = rows.Where(r => r.LatencyMs.HasValue).Select(r => r.LatencyMs!.Value).ToList();
        summary.AvgLatencyMs = latencies.Count == 0
            ? null
            : (int)Math.Round(latencies.Average());

        summary.ByDay = rows
            .GroupBy(r => r.RequestedAt / SecondsPerDay)
            .OrderBy(g => g.Key)
            .Select(g => new AiUsageDayBucket
            {
                Date = DateTimeOffset.FromUnixTimeSeconds(g.Key * SecondsPerDay)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Calls = g.Count(),
                InputTokens = g.Sum(r => (long)(r.InputTokens ?? 0)),
                OutputTokens = g.Sum(r => (long)(r.OutputTokens ?? 0)),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
                Failed = g.Count(r => !r.Success),
            })
            .ToList();

        summary.ByModel = rows
            .GroupBy(r => r.Model)
            .Select(g => new AiUsageModelBucket
            {
                Model = g.Key,
                Calls = g.Count(),
                InputTokens = g.Sum(r => (long)(r.InputTokens ?? 0)),
                OutputTokens = g.Sum(r => (long)(r.OutputTokens ?? 0)),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
            })
            .OrderByDescending(b => b.Calls)
            .ToList();

        summary.ByUser = rows
            .GroupBy(r => (r.UserGuid, r.UserEmail))
            .Select(g => new AiUsageUserBucket
            {
                UserId = g.Key.UserGuid.ToString(),
                UserEmail = g.Key.UserEmail,
                Calls = g.Count(),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
            })
            .OrderByDescending(b => b.Calls)
            .ThenBy(b => b.UserEmail)
            .Take(10)
            .ToList();

        return summary;
    }
}