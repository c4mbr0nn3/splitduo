using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SplitDuo.Api.Health;

public static class HealthResponseWriter
{
    // Status-only keeps the response body free of per-check names and error
    // descriptions so it can be exposed to unauthenticated probes.
    public static async Task WriteStatusOnly(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = System.Net.Mime.MediaTypeNames.Application.Json;
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    }

    // Detailed variant is intended for authenticated operators: it mirrors the
    // default UIWriter shape, including the cosmetic unix-timestamp field.
    public static async Task WriteDetailed(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = System.Net.Mime.MediaTypeNames.Application.Json;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), description = e.Value.Description }),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }));
    }
}