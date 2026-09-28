namespace SplitDuo.Api.Features.Ai.Dto;

public class AiUsageSummaryDto
{
    public int TotalCalls { get; set; }
    public int SuccessfulCalls { get; set; }
    public int FailedCalls { get; set; }
    public double SuccessRate { get; set; }         // 0..1; 0 when TotalCalls == 0
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public long TotalTokens { get; set; }
    public int? AvgLatencyMs { get; set; }          // null when no row has latency
    public List<AiUsageDayBucket> ByDay { get; set; } = [];
    public List<AiUsageModelBucket> ByModel { get; set; } = [];
    public List<AiUsageUserBucket> ByUser { get; set; } = [];
}

public class AiUsageDayBucket
{
    public string Date { get; set; } = "";          // "yyyy-MM-dd" UTC
    public int Calls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long TotalTokens { get; set; }
    public int Failed { get; set; }
}

public class AiUsageModelBucket
{
    public string Model { get; set; } = "";
    public int Calls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long TotalTokens { get; set; }
}

public class AiUsageUserBucket
{
    public string UserId { get; set; } = "";        // user's Guid
    public string? UserEmail { get; set; }
    public int Calls { get; set; }
    public long TotalTokens { get; set; }
}