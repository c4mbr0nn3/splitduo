using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;
using SplitDuo.Core.Services.BackgroundJobs;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class EmailNotificationProcessingJobTests
{
    #region Helpers

    private static EmailNotificationProcessingJob CreateJob(
        INotificationService notificationService,
        IUnitOfWork unitOfWork)
    {
        return new EmailNotificationProcessingJob(
            NullLogger<EmailNotificationProcessingJob>.Instance,
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
            Body = $"Body {id}"
        };
    }

    #endregion

    #region Tests

    [Fact]
    public async Task Execute_SendsUnsentNotifications_AndSaves()
    {
        var notificationService = Substitute.For<INotificationService>();
        notificationService.GetUnsentNotifications()
            .Returns(Result<List<Notification>>.Success(
                [CreateNotification(1), CreateNotification(2)]));
        notificationService.SendAsync(Arg.Any<Notification>())
            .Returns(Result.Success());

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var job = CreateJob(notificationService, unitOfWork);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await notificationService.Received(2).SendAsync(Arg.Any<Notification>());
        await unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_NoUnsentNotifications_ReturnsEarly()
    {
        var notificationService = Substitute.For<INotificationService>();
        notificationService.GetUnsentNotifications()
            .Returns(Result<List<Notification>>.Success([]));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var job = CreateJob(notificationService, unitOfWork);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await notificationService.DidNotReceive().SendAsync(Arg.Any<Notification>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_SendFailure_ContinuesToNext()
    {
        var notificationService = Substitute.For<INotificationService>();
        notificationService.GetUnsentNotifications()
            .Returns(Result<List<Notification>>.Success(
                [CreateNotification(1), CreateNotification(2)]));
        notificationService.SendAsync(Arg.Is<Notification>(n => n.Id == 1))
            .Returns(Result.BadRequest("fail"));
        notificationService.SendAsync(Arg.Is<Notification>(n => n.Id == 2))
            .Returns(Result.Success());

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var job = CreateJob(notificationService, unitOfWork);

        await job.Execute(Substitute.For<IJobExecutionContext>());

        await notificationService.Received(2).SendAsync(Arg.Any<Notification>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_GetUnsentFailure_Throws()
    {
        var notificationService = Substitute.For<INotificationService>();
        notificationService.GetUnsentNotifications()
            .Returns(Result<List<Notification>>.InternalServerError("DB error"));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var job = CreateJob(notificationService, unitOfWork);

        var exception = await Assert.ThrowsAsync<Exception>(
            () => job.Execute(Substitute.For<IJobExecutionContext>()));

        Assert.Contains("DB error", exception.Message);
        await notificationService.DidNotReceive().SendAsync(Arg.Any<Notification>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}
