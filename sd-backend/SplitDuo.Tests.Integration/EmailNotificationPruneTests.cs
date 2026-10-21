using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Tests.Integration;

public class EmailNotificationPruneTests : IntegrationTest
{
    public EmailNotificationPruneTests(SplitDuoApiFactory factory) : base(factory) { }

    [Fact]
    public async Task GetPrunableNotifications_ReturnsOnlySentOldAndExhaustedUnsent()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = Factory.TimeProvider.GetUtcNow().ToUnixTimeSeconds();

        // Seed 5 notifications directly via the DbContext
        int idSentOld, idRetriesExhaustedOld, idRetriesExhaustedYoung, idSentRecent, idUnsentRecent;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var sentOld = new Notification
            {
                To = "a@localhost", Subject = "A", Body = "A",
                SentAt = now - 31L * 24 * 60 * 60,
                CreatedAt = now - 40L * 24 * 60 * 60,
                RetryCount = 0,
            }; // prunable: sent and older than 30 days
            var retriesExhaustedOld = new Notification
            {
                To = "b@localhost", Subject = "B", Body = "B",
                SentAt = null,
                RetryCount = 3,
                CreatedAt = now - 31L * 24 * 60 * 60,
            }; // prunable: unsent, retries exhausted, created before threshold
            var retriesExhaustedYoung = new Notification
            {
                To = "c@localhost", Subject = "C", Body = "C",
                SentAt = null,
                RetryCount = 2,
                CreatedAt = now - 31L * 24 * 60 * 60,
            }; // NOT prunable: retries not exhausted
            var sentRecent = new Notification
            {
                To = "d@localhost", Subject = "D", Body = "D",
                SentAt = now - 10L * 24 * 60 * 60,
                CreatedAt = now - 10L * 24 * 60 * 60,
                RetryCount = 0,
            }; // NOT prunable: sent but recent
            var unsentRecent = new Notification
            {
                To = "e@localhost", Subject = "E", Body = "E",
                SentAt = null,
                RetryCount = 3,
                CreatedAt = now - 10L * 24 * 60 * 60,
            }; // NOT prunable: exhausted but recent

            db.Notifications.AddRange(sentOld, retriesExhaustedOld, retriesExhaustedYoung, sentRecent, unsentRecent);
            await db.SaveChangesAsync(ct);

            idSentOld = sentOld.Id;
            idRetriesExhaustedOld = retriesExhaustedOld.Id;
            idRetriesExhaustedYoung = retriesExhaustedYoung.Id;
            idSentRecent = sentRecent.Id;
            idUnsentRecent = unsentRecent.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var result = await notificationService.GetPrunableNotifications();

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);

            var ids = result.Value!.Select(n => n.Id).ToHashSet();
            Assert.Contains(idSentOld, ids);
            Assert.Contains(idRetriesExhaustedOld, ids);
            Assert.DoesNotContain(idRetriesExhaustedYoung, ids);
            Assert.DoesNotContain(idSentRecent, ids);
            Assert.DoesNotContain(idUnsentRecent, ids);
            Assert.Equal(2, ids.Count);
        }
    }
}