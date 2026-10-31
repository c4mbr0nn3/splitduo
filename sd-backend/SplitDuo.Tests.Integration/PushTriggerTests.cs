using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Services;
using SplitDuo.Core.Services.BackgroundJobs;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Expenses;
using SplitDuo.Tests.Integration.Support;
using Xunit;

namespace SplitDuo.Tests.Integration;

/// <summary>
/// Pins the three places the application decides to send a push notification. Each guard
/// protects against a notification that cannot be recalled once delivered: backfilled
/// recurring periods spamming the group, the wrong people learning about an invitation,
/// and a new member being told about their own addition.
///
/// The recorder observes <b>selection</b> (who the trigger chose), not delivery — the real
/// sender additionally skips deleted users and users without subscriptions.
/// </summary>
[Collection("Integration")]
public class PushTriggerTests : IntegrationTest
{
    private const string RecurringBodyKey = "PushBodyRecurringExpense";
    private const string InvitationAcceptedBodyKey = "PushBodyInvitationAccepted";
    private const string MemberAddedBodyKey = "PushBodyMemberAdded";

    public PushTriggerTests(SplitDuoApiFactory factory) : base(factory) { }

    /// <summary>
    /// Runs the generation job the way the suite already runs it: hand-built from a scope,
    /// because the test host removes the Quartz hosted service. The sender comes from DI,
    /// which the factory has replaced with the recorder.
    /// </summary>
    private async Task RunGenerationJobAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var job = new RecurringExpenseGenerationJob(
            sp.GetRequiredService<ILogger<RecurringExpenseGenerationJob>>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ICacheInvalidator>(),
            sp.GetRequiredService<IExpenseCreationService>(),
            sp.GetRequiredService<IPushNotificationService>());

        await job.Execute(null!);
    }

    // ─── Recurring expense ──────────────────────────────────────────────────

    [Fact]
    public async Task Job_BackfilledPeriods_DoNotNotify_ButCurrentPeriodDoes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var anchor = JobToday.AddDays(-21); // same weekday as today (21 % 7 == 0)

        await client.CreateTemplateAsync(group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } }, anchor);

        await RunGenerationJobAsync();

        // Guard the setup before asserting sends: a run that generated nothing would
        // otherwise make "exactly one send" meaningless.
        var (expenses, instances) = await RecurringTestHelpers.CountRecurringRowsAsync(Factory.Services);
        Assert.Equal(4, instances);
        Assert.Equal(4, expenses);

        var send = Assert.Single(Factory.PushRecorder.Sends);
        Assert.Equal(await TestDbSeeder.GetUserIntIdAsync(Factory.Services, "admin@splitduo.local"), send.UserId);
        Assert.Equal(RecurringBodyKey, send.Payload.BodyKey);
        Assert.Equal("Recurring Test", send.Payload.BodyArgs[0]);
        Assert.Equal($"/groups/{group.Id}", send.Payload.Url);
        Assert.StartsWith($"group-{group.Id}-recurring-", send.Payload.Tag);

        // Idempotency: a second run generates nothing and therefore notifies nobody.
        await RunGenerationJobAsync();
        Assert.Single(Factory.PushRecorder.Sends);
    }

    [Fact]
    public async Task Job_NotifiesEveryActiveMember_AndStopsNotifyingARemovedOne()
    {
        var ct = TestContext.Current.CancellationToken;
        using var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();
        var admin = await adminClient.GetCurrentUserAsync();
        var (memberEmail, _, _) = await SeedSecondMemberAsync(adminClient, group.Id);

        var adminIntId = await TestDbSeeder.GetUserIntIdAsync(Factory.Services, "admin@splitduo.local");
        var memberIntId = await TestDbSeeder.GetUserIntIdAsync(Factory.Services, memberEmail);

        await adminClient.CreateTemplateAsync(group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } }, JobToday);

        await RunGenerationJobAsync();

        var recipients = Factory.PushRecorder.Sends.Select(s => s.UserId).ToList();
        Assert.Equal(2, recipients.Count);
        Assert.Equal(new[] { adminIntId, memberIntId }.Order(), recipients.Order());
        Assert.All(Factory.PushRecorder.Sends, s => Assert.Equal(RecurringBodyKey, s.Payload.BodyKey));

        // Remove the second member (soft delete, so the recipient query must filter it),
        // then let a second template fire. Splits exclude the removed member on purpose:
        // a split for a non-member pauses the template instead of generating.
        var removeResponse = await adminClient.DeleteAsync(
            $"/api/v1/groups/{group.Id}/members/{await TestDbSeeder.GetUserGuidAsync(Factory.Services, memberEmail)}",
            ct);
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);

        await adminClient.CreateTemplateAsync(group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } }, JobToday);

        Factory.PushRecorder.Reset();
        await RunGenerationJobAsync();

        // Guard the setup again: exactly one new instance means the removed member was the
        // only thing that could have changed the recipient count.
        var (_, instances) = await RecurringTestHelpers.CountRecurringRowsAsync(Factory.Services);
        Assert.Equal(2, instances); // one per template

        var afterRemoval = Assert.Single(Factory.PushRecorder.Sends);
        Assert.Equal(adminIntId, afterRemoval.UserId);
    }

    // ─── Invitations ────────────────────────────────────────────────────────

    [Fact]
    public async Task InvitationAccepted_NotifiesEveryDistinctInviter_NotJustTheTokenOwner()
    {
        var ct = TestContext.Current.CancellationToken;

        // Two admins, two groups, one invitee address.
        using var adminClient = await CreateAuthenticatedClientAsync();
        var groupA = await adminClient.CreateGroupAsync("Group A");
        await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupA.Id}/invitations",
            new { email = "multi-accept@localhost" }, ct);

        await TestDbSeeder.SeedUserAsync(Factory.Services, "inviter2@localhost", "changeme123", "Inviter", "Two");
        using var inviter2Client = await CreateAuthenticatedClientAsync("inviter2@localhost");
        var groupB = await inviter2Client.CreateGroupAsync("Group B");
        await inviter2Client.PostAsJsonAsync($"/api/v1/groups/{groupB.Id}/invitations",
            new { email = "multi-accept@localhost" }, ct);

        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "multi-accept@localhost");

        var accept = await Client.PostAsJsonAsync("/api/v1/invitations/accept", new
        {
            token,
            firstName = "Multi",
            lastName = "Accepter",
            password = "StrongPass1!",
            confirmPassword = "StrongPass1!",
            uiLanguage = "en",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // Accepting resolves every pending invitation for the address, so both inviters
        // hear about it. A regression that only notified the token's inviter would pass a
        // single-invitation test but strand the other group's inviter.
        var sends = Factory.PushRecorder.Sends;
        Assert.Equal(2, sends.Count);

        var adminId = await TestDbSeeder.GetUserIntIdAsync(Factory.Services, "admin@splitduo.local");
        var inviter2Id = await TestDbSeeder.GetUserIntIdAsync(Factory.Services, "inviter2@localhost");
        Assert.Equal(new[] { adminId, inviter2Id }.Order(), sends.Select(s => s.UserId).Order());

        Assert.Equal(
            new[] { $"/groups/{groupA.Id}", $"/groups/{groupB.Id}" }.Order(),
            sends.Select(s => s.Payload.Url).Order());

        Assert.All(sends, send =>
        {
            Assert.Equal(InvitationAcceptedBodyKey, send.Payload.BodyKey);
            Assert.Equal("Multi", send.Payload.BodyArgs[0]);
        });
    }

    [Fact]
    public async Task ExistingUserInvitedToGroup_NotifiesOtherMembers_NotTheNewMemberOrInviter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();
        var admin = await adminClient.GetCurrentUserAsync();

        // A third member who should receive the notification.
        var (thirdEmail, _, _) = await SeedSecondMemberAsync(adminClient, group.Id);

        // An existing account that is NOT in the group yet; inviting it by email adds it
        // directly and pushes to the other members.
        await TestDbSeeder.SeedUserAsync(Factory.Services, "existing-invitee@localhost", "changeme123", "Existing", "User");
        var invite = await adminClient.PostAsJsonAsync($"/api/v1/groups/{group.Id}/invitations",
            new { email = "existing-invitee@localhost" }, ct);
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);

        var adminId = await TestDbSeeder.GetUserIntIdAsync(Factory.Services, "admin@splitduo.local");
        var thirdId = await TestDbSeeder.GetUserIntIdAsync(Factory.Services, thirdEmail);
        var newMemberGuid = await TestDbSeeder.GetUserGuidAsync(Factory.Services, "existing-invitee@localhost");

        var send = Assert.Single(Factory.PushRecorder.Sends);
        Assert.Equal(thirdId, send.UserId); // not the inviter, not the new member
        Assert.NotEqual(adminId, send.UserId);
        Assert.Equal(MemberAddedBodyKey, send.Payload.BodyKey);
        Assert.Equal("Existing", send.Payload.BodyArgs[0]);
        Assert.Equal($"/groups/{group.Id}", send.Payload.Url);
        Assert.Equal($"group-{group.Id}-member-added-{newMemberGuid}", send.Payload.Tag);
    }
}
