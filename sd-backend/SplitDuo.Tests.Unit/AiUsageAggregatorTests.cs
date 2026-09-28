using SplitDuo.Api.Features.Ai.Services;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class AiUsageAggregatorTests
{
    private static AiUsageRowProjection Row(
        long requestedAt,
        int? inputTokens = null,
        int? outputTokens = null,
        int? totalTokens = null,
        int? latencyMs = null,
        bool success = true,
        string model = "gpt-test",
        Guid? userGuid = null,
        string? userEmail = "u@example.com") => new(
        requestedAt, inputTokens, outputTokens, totalTokens,
        latencyMs, success, model, userGuid ?? Guid.NewGuid(), userEmail);

    [Fact]
    public void Aggregate_EmptyList_ReturnsZeroedSummary()
    {
        var summary = AiUsageAggregator.Aggregate([]);

        Assert.Equal(0, summary.TotalCalls);
        Assert.Equal(0, summary.SuccessfulCalls);
        Assert.Equal(0, summary.FailedCalls);
        Assert.Equal(0.0, summary.SuccessRate);
        Assert.Equal(0, summary.TotalInputTokens);
        Assert.Equal(0, summary.TotalOutputTokens);
        Assert.Equal(0, summary.TotalTokens);
        Assert.Null(summary.AvgLatencyMs);
        Assert.Empty(summary.ByDay);
        Assert.Empty(summary.ByModel);
        Assert.Empty(summary.ByUser);
    }

    [Fact]
    public void Aggregate_NullTokensCountedAsZero()
    {
        var rows = new List<AiUsageRowProjection>
        {
            Row(1770139140, inputTokens: null, outputTokens: 10, totalTokens: 20),
            Row(1770139141, inputTokens: 5, outputTokens: null, totalTokens: null),
        };

        var summary = AiUsageAggregator.Aggregate(rows);

        Assert.Equal(5, summary.TotalInputTokens);
        Assert.Equal(10, summary.TotalOutputTokens);
        Assert.Equal(20, summary.TotalTokens);
    }

    [Fact]
    public void Aggregate_UtcDayBucketing_LandsInExpectedDate()
    {
        // Verified: date -u -d @1770076800 -> 2026-02-03T00:00:00Z (midnight UTC)
        var rows = new List<AiUsageRowProjection> { Row(1770076800, totalTokens: 7) };

        var summary = AiUsageAggregator.Aggregate(rows);

        var day = Assert.Single(summary.ByDay);
        Assert.Equal("2026-02-03", day.Date);
        Assert.Equal(1, day.Calls);
        Assert.Equal(7, day.TotalTokens);
        Assert.Equal(0, day.Failed);
    }

    [Fact]
    public void Aggregate_RowsStraddlingUtcMidnight_ProduceTwoBucketsAscending()
    {
        // Verified: 1770076799 -> 2026-02-02T23:59:59Z; 1770076800 -> 2026-02-03T00:00:00Z
        var rows = new List<AiUsageRowProjection>
        {
            Row(1770076800, totalTokens: 3),
            Row(1770076799, totalTokens: 4),
        };

        var summary = AiUsageAggregator.Aggregate(rows);

        Assert.Equal(2, summary.ByDay.Count);
        Assert.Equal("2026-02-02", summary.ByDay[0].Date);
        Assert.Equal("2026-02-03", summary.ByDay[1].Date);
        Assert.Equal(1, summary.ByDay[0].Calls);
        Assert.Equal(1, summary.ByDay[1].Calls);
        Assert.Equal(4, summary.ByDay[0].TotalTokens);
        Assert.Equal(3, summary.ByDay[1].TotalTokens);
    }

    [Fact]
    public void Aggregate_SuccessRate_ComputedAsRatio()
    {
        var rows = new List<AiUsageRowProjection>
        {
            Row(1770076800, success: true),
            Row(1770076801, success: false),
        };

        var summary = AiUsageAggregator.Aggregate(rows);

        Assert.Equal(2, summary.TotalCalls);
        Assert.Equal(1, summary.SuccessfulCalls);
        Assert.Equal(1, summary.FailedCalls);
        Assert.Equal(0.5, summary.SuccessRate);
    }

    [Fact]
    public void Aggregate_AvgLatency_SkipsNulls()
    {
        var rows = new List<AiUsageRowProjection>
        {
            Row(1770076800, latencyMs: 100),
            Row(1770076801, latencyMs: null),
            Row(1770076802, latencyMs: 200),
        };

        var summary = AiUsageAggregator.Aggregate(rows);

        Assert.Equal(150, summary.AvgLatencyMs);
    }

    [Fact]
    public void Aggregate_AllNullLatency_ReturnsNull()
    {
        var rows = new List<AiUsageRowProjection>
        {
            Row(1770076800, latencyMs: null),
            Row(1770076801, latencyMs: null),
        };

        var summary = AiUsageAggregator.Aggregate(rows);

        Assert.Null(summary.AvgLatencyMs);
    }

    [Fact]
    public void Aggregate_ByUser_CappedAtTen_SortedByCallCount_HeavyUserFirst()
    {
        var heavy = Guid.NewGuid();
        var rows = new List<AiUsageRowProjection>();
        for (var i = 0; i < 15; i++) rows.Add(Row(1770076800 + i, userGuid: heavy, userEmail: "heavy@example.com"));
        for (var i = 0; i < 10; i++) rows.Add(Row(1770076800 + i, userGuid: Guid.NewGuid(), userEmail: $"u{i}@example.com"));

        var summary = AiUsageAggregator.Aggregate(rows);

        Assert.Equal(10, summary.ByUser.Count);
        Assert.Equal(heavy.ToString(), summary.ByUser[0].UserId);
        Assert.Equal("heavy@example.com", summary.ByUser[0].UserEmail);
        Assert.Equal(15, summary.ByUser[0].Calls);
        Assert.True(summary.ByUser[0].Calls >= summary.ByUser[1].Calls);
    }
}