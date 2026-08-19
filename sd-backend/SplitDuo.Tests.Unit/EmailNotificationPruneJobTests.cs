using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;
using SplitDuo.Core.Services.BackgroundJobs;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class EmailNotificationPruneJobTests
{
    #region Helpers

    private static EmailNotificationPruneJob CreateJob(
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        return new EmailNotificationPruneJob(
            NullLogger<EmailNotificationPruneJob>.Instance,
            notificationService,
            unitOfWork);
    }

    private static Notification CreateNotification(int id)
    {
        return new Notification
        {
            Id = id,
            To = $"test{id}@splitduo.local",
            Subject = $"Subject {id}",
            Body = $"Body {id}",
            SentAt = 1_700_000_000,
            RetryCount = 3
        };
    }

    #endregion

    #region Tests

    [Fact]
    public async Task Execute_PrunesPrunableNotifications_AndSaves()
    {
        var notificationService = Substitute.For<INotificationService>();
        notificationService.GetPrunableNotifications()
            .Returns(Result<List<Notification>>.Success(
                [CreateNotification(1), CreateNotification(2)]));
        notificationService.Prune(Arg.Any<Notification>())
            .Returns(Result.Success());

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var job = CreateJob(notificationService, unitOfWork);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        notificationService.Received(2).Prune(Arg.Any<Notification>());
        await unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoPrunableNotifications_ReturnsEarly()
    {
        var notificationService = Substitute.For<INotificationService>();
        notificationService.GetPrunableNotifications()
            .Returns(Result<List<Notification>>.Success([]));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var job = CreateJob(notificationService, unitOfWork);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        notificationService.DidNotReceive().Prune(Arg.Any<Notification>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PruneFailure_ContinuesToNext()
    {
        var notificationService = Substitute.For<INotificationService>();
        notificationService.GetPrunableNotifications()
            .Returns(Result<List<Notification>>.Success(
                [CreateNotification(1), CreateNotification(2)]));
        notificationService.Prune(Arg.Is<Notification>(n => n.Id == 1))
            .Returns(Result.BadRequest("fail"));
        notificationService.Prune(Arg.Is<Notification>(n => n.Id == 2))
            .Returns(Result.Success());

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var job = CreateJob(notificationService, unitOfWork);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        notificationService.Received(2).Prune(Arg.Any<Notification>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}
