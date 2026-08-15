using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Factories;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.BackgroundJobs;
using SplitDuo.Core.Services.Imports;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class ImportProcessingJobTests
{
    private static readonly FakeTimeProvider TestTimeProvider =
        new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"import-processing-job-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static IJobExecutionContext CreateJobExecutionContext(string importGuid, string importType)
    {
        var jobDataMap = new JobDataMap
        {
            ["ImportGuid"] = importGuid,
            ["ImportType"] = importType
        };

        var jobDetail = Substitute.For<IJobDetail>();
        jobDetail.JobDataMap.Returns(jobDataMap);

        var context = Substitute.For<IJobExecutionContext>();
        context.JobDetail.Returns(jobDetail);

        return context;
    }

    private static (Import Import, Group Group) SeedImport(AppDbContext context, Guid importGuid, Guid groupGuid)
    {
        var user = new User
        {
            Id = 1,
            Email = "test@splitduo.local",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        var group = new Group
        {
            Id = 1,
            Guid = groupGuid,
            Name = "Test Group",
            CreatedBy = 1
        };

        var import = new Import
        {
            Id = 1,
            Guid = importGuid,
            GroupId = 1,
            UserId = 1,
            FileName = "test.csv",
            FileHash = "test-hash",
            ImportDate = new DateOnly(2026, 1, 1),
            Status = ImportStatus.Pending,
            ImportType = ImportType.SplitDuo,
            TempFile = [1, 2, 3]
        };

        context.Users.Add(user);
        context.Groups.Add(group);
        context.Imports.Add(import);
        context.SaveChanges();

        return (import, group);
    }

    private static ImportProcessingJob CreateJob(
        AppDbContext context,
        IImportsService importsService,
        ICacheInvalidator cacheInvalidator)
    {
        var unitOfWork = new UnitOfWork(context);

        var factory = Substitute.For<IImportServiceFactory>();
        factory.GetImportService(ImportType.SplitDuo).Returns(importsService);

        return new ImportProcessingJob(
            NullLogger<ImportProcessingJob>.Instance,
            unitOfWork,
            factory,
            TestTimeProvider,
            cacheInvalidator);
    }

    [Fact]
    public async Task Execute_SuccessfulImport_InvalidatesGroupCache()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        await using var context = CreateContext();
        var (_, group) = SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        importsService.ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Result<int>.Success(10));

        var cacheInvalidator = Substitute.For<ICacheInvalidator>();
        var job = CreateJob(context, importsService, cacheInvalidator);

        await job.Execute(CreateJobExecutionContext(importGuid.ToString(), "SplitDuo"));

        await cacheInvalidator.Received(1).InvalidateGroupAsync(group.Guid.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_FailedImport_DoesNotInvalidateGroupCache()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        await using var context = CreateContext();
        SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        importsService.ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Result<int>.BadRequest("Import failed"));

        var cacheInvalidator = Substitute.For<ICacheInvalidator>();
        var job = CreateJob(context, importsService, cacheInvalidator);

        await job.Execute(CreateJobExecutionContext(importGuid.ToString(), "SplitDuo"));

        await cacheInvalidator.DidNotReceive().InvalidateGroupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ImportNotFound_DoesNotInvalidateGroupCache()
    {
        await using var context = CreateContext();

        var cacheInvalidator = Substitute.For<ICacheInvalidator>();
        var job = CreateJob(context, Substitute.For<IImportsService>(), cacheInvalidator);

        await job.Execute(CreateJobExecutionContext(Guid.NewGuid().ToString(), "SplitDuo"));

        await cacheInvalidator.DidNotReceive().InvalidateGroupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
