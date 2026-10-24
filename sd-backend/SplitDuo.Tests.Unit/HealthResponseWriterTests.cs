using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SplitDuo.Api.Health;
using Xunit;

namespace SplitDuo.Tests.Unit;

public sealed class HealthResponseWriterTests
{
    private static (DefaultHttpContext Context, MemoryStream Body) CreateContext()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = body;
        return (context, body);
    }

    private static HealthReport Report() => new(
        new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new(HealthStatus.Unhealthy, "connection refused", TimeSpan.Zero, null, null)
        },
        TimeSpan.Zero);

    [Fact]
    public async Task WriteStatusOnly_EmitsOnlyTheAggregateStatus()
    {
        var (context, body) = CreateContext();

        await HealthResponseWriter.WriteStatusOnly(context, Report());
        using var json = JsonDocument.Parse(body.ToArray());

        Assert.Equal("Unhealthy", json.RootElement.GetProperty("status").GetString());
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.DoesNotContain("database", Encoding.UTF8.GetString(body.ToArray()));
        Assert.DoesNotContain("connection refused", Encoding.UTF8.GetString(body.ToArray()));
    }

    [Fact]
    public async Task WriteDetailed_IncludesCheckNameStatusAndDescription()
    {
        var (context, body) = CreateContext();

        await HealthResponseWriter.WriteDetailed(context, Report());
        using var json = JsonDocument.Parse(body.ToArray());

        var check = json.RootElement.GetProperty("checks").GetProperty("database");
        Assert.Equal("Unhealthy", check.GetProperty("status").GetString());
        Assert.Equal("connection refused", check.GetProperty("description").GetString());
        Assert.Equal(System.Net.Mime.MediaTypeNames.Application.Json, context.Response.ContentType);
    }
}