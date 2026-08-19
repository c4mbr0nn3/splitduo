using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Imports;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class AbstractImportServiceTests
{
    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }

    private static Import SeedImport(AppDbContext context, Guid importGuid)
    {
        context.Users.Add(new User
        {
            Id = 1,
            Email = "test@splitduo.local",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        });

        context.Groups.Add(new Group
        {
            Id = 1,
            Guid = Guid.NewGuid(),
            Name = "Test Group",
            CreatedBy = 1
        });

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

        context.Imports.Add(import);
        context.SaveChanges();

        return import;
    }

    private static TestImportService CreateService(
        IUnitOfWork unitOfWork,
        IImportValidatorService validatorService,
        ISchedulerFactory schedulerFactory)
    {
        return new TestImportService(
            unitOfWork,
            validatorService,
            schedulerFactory,
            NullLogger<TestImportService>.Instance,
            TimeProvider.System);
    }

    #region CreateImportJobAsync

    [Fact]
    public async Task CreateImportJobAsync_Throws_ReturnsInternalServerError()
    {
        var imports = Substitute.For<DbSet<Import>>();
        imports.AddAsync(Arg.Any<Import>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Imports.Returns(imports);

        var service = CreateService(
            unitOfWork,
            Substitute.For<IImportValidatorService>(),
            Substitute.For<ISchedulerFactory>());

        var result = await service.CreateImportJobAsync(
            Substitute.For<IFormFile>(),
            1,
            1,
            new ImportAnalysisDto { FileHash = "test-hash" });

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal("Failed to create import job", result.Error);
    }

    #endregion

    #region UpdateImportMappingsAsync

    [Fact]
    public async Task UpdateImportMappingsAsync_ImportNotFound_ReturnsNotFound()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);

        var service = CreateService(
            new UnitOfWork(context),
            Substitute.For<IImportValidatorService>(),
            Substitute.For<ISchedulerFactory>());

        var result = await service.UpdateImportMappingsAsync(Guid.NewGuid(), new ImportMappingDto());

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("Import not found", result.Error);
    }

    [Fact]
    public async Task UpdateImportMappingsAsync_Throws_ReturnsInternalServerError()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var importGuid = Guid.NewGuid();
        SeedImport(context, importGuid);

        var validatorService = Substitute.For<IImportValidatorService>();
        validatorService.ValidateMappingConfigurationAsync(Arg.Any<ImportMappingDto>(), Arg.Any<int>())
            .ThrowsAsync(new InvalidOperationException("mapping validation failed"));

        var service = CreateService(
            new UnitOfWork(context),
            validatorService,
            Substitute.For<ISchedulerFactory>());

        var result = await service.UpdateImportMappingsAsync(importGuid, new ImportMappingDto());

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal("mapping validation failed", result.Error);
    }

    #endregion

    #region TriggerImportJobAsync

    [Fact]
    public async Task TriggerImportJobAsync_ImportNotFound_ReturnsNotFound()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);

        var service = CreateService(
            new UnitOfWork(context),
            Substitute.For<IImportValidatorService>(),
            Substitute.For<ISchedulerFactory>());

        var result = await service.TriggerImportJobAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("Import not found", result.Error);
    }

    [Fact]
    public async Task TriggerImportJobAsync_Throws_ReturnsInternalServerError()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var importGuid = Guid.NewGuid();
        SeedImport(context, importGuid);

        var schedulerFactory = Substitute.For<ISchedulerFactory>();
        schedulerFactory.GetScheduler(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("scheduler failed"));

        var service = CreateService(
            new UnitOfWork(context),
            Substitute.For<IImportValidatorService>(),
            schedulerFactory);

        var result = await service.TriggerImportJobAsync(importGuid);

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal("scheduler failed", result.Error);
    }

    #endregion
}

internal class TestImportService : AbstractImportService<TestImportService>
{
    public TestImportService(
        IUnitOfWork unitOfWork,
        IImportValidatorService validatorService,
        ISchedulerFactory schedulerFactory,
        ILogger<TestImportService> logger,
        TimeProvider timeProvider)
        : base(ImportType.SplitDuo, unitOfWork, validatorService, schedulerFactory, logger, timeProvider)
    {
    }

    public override Task<Result<ImportAnalysisDto>> AnalyzeFileAsync(IFormFile file)
        => throw new NotImplementedException();

    public override Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId)
        => throw new NotImplementedException();
}
