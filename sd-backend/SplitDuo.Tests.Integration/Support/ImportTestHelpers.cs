using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Factories;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.BackgroundJobs;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// Static helpers for import integration tests — shared across all import test files.
/// </summary>
public static class ImportTestHelpers
{
    /// <summary>
    /// Uploads a CSV to POST /imports/analyze and returns the response.
    /// </summary>
    public static async Task<HttpResponseMessage> AnalyzeAsync(
        HttpClient client, string groupId, string csv, int importTypeId = (int)ImportType.SplitDuo,
        string fileName = "import.csv")
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new MultipartFormDataContent();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(importTypeId.ToString()), "ImportTypeId");

        return await client.PostAsync(
            $"/api/v1/groups/{groupId}/imports/analyze", content, ct);
    }

    /// <summary>
    /// Constructs an ImportProcessingJob with its DI dependencies. The job is not registered as
    /// a concrete service (Quartz instantiates it via the job type), so we build it manually.
    /// </summary>
    public static ImportProcessingJob CreateImportProcessingJob(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<ImportProcessingJob>>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var factory = services.GetRequiredService<IImportServiceFactory>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var cacheInvalidator = services.GetRequiredService<ICacheInvalidator>();
        return new ImportProcessingJob(logger, unitOfWork, factory, timeProvider, cacheInvalidator);
    }

    /// <summary>
    /// Runs the import processing job directly (bypassing the Quartz scheduler thread, which is
    /// removed in the test host). Saves the mapping first via the HTTP endpoint, then invokes
    /// the job's Execute method with a synthetic IJobExecutionContext.
    /// </summary>
    public static async Task RunImportJobAsync(IServiceProvider services, string importId, ImportType importType)
    {
        var job = CreateImportProcessingJob(services);
        var scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var jobData = new JobDataMap
        {
            ["ImportGuid"] = importId,
            ["ImportType"] = importType.ToString(),
        };
        var jobDetail = JobBuilder.Create<ImportProcessingJob>()
            .WithIdentity($"test-import-{importId}")
            .UsingJobData(jobData)
            .Build();
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"test-import-trigger-{importId}")
            .StartNow()
            .Build();
        await job.Execute(new TestJobExecutionContext(scheduler, jobDetail, trigger));
    }
}

/// <summary>
/// Minimal IJobExecutionContext for directly invoking ImportProcessingJob.Execute in tests.
/// Only the JobDetail and JobDataMap are used by ImportProcessingJob; other members are stubs.
/// </summary>
public class TestJobExecutionContext(IScheduler scheduler, IJobDetail jobDetail, ITrigger trigger) : IJobExecutionContext
{
    public IScheduler Scheduler => scheduler;
    public IJobDetail JobDetail => jobDetail;
    public ITrigger Trigger => trigger;
    public ICalendar? Calendar => null;
    public bool Recovering => false;
    public TriggerKey RecoveringTriggerKey => new("test");
    public int RefireCount => 0;
    public JobDataMap JobDataMap => jobDetail.JobDataMap;
    public JobDataMap MergedJobDataMap => jobDetail.JobDataMap;
    public IJob JobInstance => null!;
    public CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledFireTimeUtc => DateTimeOffset.UtcNow;
    public DateTimeOffset? PreviousFireTimeUtc => null;
    public DateTimeOffset? NextFireTimeUtc => null;
    public string FireInstanceId => "test-fire-instance";
    public object? Result { get; set; }
    public TimeSpan JobRunTime => TimeSpan.Zero;
    public void Put(object key, object value) { }
    public object? Get(object key) => null;
}