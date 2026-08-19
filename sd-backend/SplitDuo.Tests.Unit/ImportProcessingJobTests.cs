using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return context;
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

    private static IJobExecutionContext CreateJobExecutionContextWithMap(Action<JobDataMap> configureMap)
    {
        var jobDataMap = new JobDataMap();
        configureMap(jobDataMap);

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
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var (_, group) = SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        importsService.ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Result<int>.Success(10));

        var cacheInvalidator = Substitute.For<ICacheInvalidator>();
        var job = CreateJob(context, importsService, cacheInvalidator);

        await job.Execute(CreateJobExecutionContext(importGuid.ToString().ToUpperInvariant(), "SplitDuo"));

        await cacheInvalidator.Received(1).InvalidateGroupAsync(group.Guid.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_FailedImport_DoesNotInvalidateGroupCache()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        importsService.ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(Result<int>.BadRequest("Import failed"));

        var cacheInvalidator = Substitute.For<ICacheInvalidator>();
        var job = CreateJob(context, importsService, cacheInvalidator);

        await job.Execute(CreateJobExecutionContext(importGuid.ToString().ToUpperInvariant(), "SplitDuo"));

        await cacheInvalidator.DidNotReceive().InvalidateGroupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ImportNotFound_DoesNotInvalidateGroupCache()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);

        var cacheInvalidator = Substitute.For<ICacheInvalidator>();
        var job = CreateJob(context, Substitute.For<IImportsService>(), cacheInvalidator);

        await job.Execute(CreateJobExecutionContext(Guid.NewGuid().ToString(), "SplitDuo"));

        await cacheInvalidator.DidNotReceive().InvalidateGroupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #region Error Paths

    [Fact]
    public async Task Execute_MissingImportGuid_ReturnsWithoutProcessing()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        var job = CreateJob(context, importsService, Substitute.For<ICacheInvalidator>());

        // Quartz's JobDataMap.GetString throws KeyNotFoundException for absent keys, so the
        // job's IsNullOrEmpty guard is exercised with an empty-string value. Both keys must be
        // present: the job reads ImportType at the top before validating ImportGuid.
        await job.Execute(CreateJobExecutionContextWithMap(map =>
        {
            map["ImportGuid"] = "";
            map["ImportType"] = "SplitDuo";
        }));

        var importFromDb = context.Imports.Single();
        Assert.Equal((int)ImportStatus.Pending, importFromDb.StatusId);
        Assert.NotNull(importFromDb.TempFile);
        Assert.Equal("", importFromDb.ErrorDetails);
        await importsService.DidNotReceive().ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Execute_InvalidImportGuid_ReturnsWithoutProcessing()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        var job = CreateJob(context, importsService, Substitute.For<ICacheInvalidator>());

        await job.Execute(CreateJobExecutionContext("not-a-guid", "SplitDuo"));

        var importFromDb = context.Imports.Single();
        Assert.Equal((int)ImportStatus.Pending, importFromDb.StatusId);
        Assert.NotNull(importFromDb.TempFile);
        Assert.Equal("", importFromDb.ErrorDetails);
        await importsService.DidNotReceive().ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Execute_ImportNotFound_ReturnsWithoutProcessing()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);

        var importsService = Substitute.For<IImportsService>();
        var job = CreateJob(context, importsService, Substitute.For<ICacheInvalidator>());

        await job.Execute(CreateJobExecutionContext(Guid.NewGuid().ToString(), "SplitDuo"));

        Assert.Empty(context.Imports);
        await importsService.DidNotReceive().ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Execute_MissingTempFile_MarksFailed()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var (import, _) = SeedImport(context, importGuid, groupGuid);
        import.TempFile = null;
        context.SaveChanges();

        var importsService = Substitute.For<IImportsService>();
        var job = CreateJob(context, importsService, Substitute.For<ICacheInvalidator>());

        await job.Execute(CreateJobExecutionContext(importGuid.ToString().ToUpperInvariant(), "SplitDuo"));

        var importFromDb = context.Imports.Single();
        Assert.Equal((int)ImportStatus.Failed, importFromDb.StatusId);
        Assert.Equal("Temporary file is missing or empty", importFromDb.ErrorDetails);
        Assert.Null(importFromDb.TempFile);
        Assert.NotNull(importFromDb.CompletedAt);
        await importsService.DidNotReceive().ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Execute_MissingImportType_MarksFailed()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        var job = CreateJob(context, importsService, Substitute.For<ICacheInvalidator>());

        // Quartz's JobDataMap.GetString throws KeyNotFoundException for absent keys, so the
        // job's IsNullOrEmpty guard is exercised with an empty-string value.
        await job.Execute(CreateJobExecutionContextWithMap(map =>
        {
            map["ImportGuid"] = importGuid.ToString().ToUpperInvariant();
            map["ImportType"] = "";
        }));

        var importFromDb = context.Imports.Single();
        Assert.Equal((int)ImportStatus.Failed, importFromDb.StatusId);
        Assert.Equal("Missing ImportType in job data", importFromDb.ErrorDetails);
        Assert.Null(importFromDb.TempFile);
        Assert.NotNull(importFromDb.CompletedAt);
        await importsService.DidNotReceive().ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Execute_InvalidImportType_MarksFailed()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        var job = CreateJob(context, importsService, Substitute.For<ICacheInvalidator>());

        await job.Execute(CreateJobExecutionContext(importGuid.ToString().ToUpperInvariant(), "InvalidType"));

        var importFromDb = context.Imports.Single();
        Assert.Equal((int)ImportStatus.Failed, importFromDb.StatusId);
        Assert.Equal("Invalid ImportType: InvalidType", importFromDb.ErrorDetails);
        Assert.Null(importFromDb.TempFile);
        Assert.NotNull(importFromDb.CompletedAt);
        await importsService.DidNotReceive().ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Execute_ProcessThrows_MarksFailed()
    {
        var importGuid = Guid.NewGuid();
        var groupGuid = Guid.NewGuid();
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedImport(context, importGuid, groupGuid);

        var importsService = Substitute.For<IImportsService>();
        importsService.ProcessImportAsync(Arg.Any<byte[]>(), Arg.Any<int>(), Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("Processing exploded"));

        var job = CreateJob(context, importsService, Substitute.For<ICacheInvalidator>());

        await job.Execute(CreateJobExecutionContext(importGuid.ToString().ToUpperInvariant(), "SplitDuo"));

        var importFromDb = context.Imports.Single();
        Assert.Equal((int)ImportStatus.Failed, importFromDb.StatusId);
        Assert.Equal("Processing exploded", importFromDb.ErrorDetails);
        Assert.Null(importFromDb.TempFile);
        Assert.NotNull(importFromDb.CompletedAt);
    }

    #endregion
}
