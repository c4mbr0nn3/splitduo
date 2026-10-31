using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;
using PushSubscription = SplitDuo.Core.Domain.Entities.PushSubscription;
using WebPush;
using Xunit;

namespace SplitDuo.Tests.Unit;

/// <summary>
/// Covers the push send path: how a push service's answer becomes a row mutation
/// (purge on 410/404, count then purge at five, reset on success), and that concurrent
/// sends are applied to their own rows.
///
/// The transport is a fake <see cref="HttpMessageHandler"/> supplied through
/// <see cref="PushHttpClient"/>'s internal constructor, so the redirect and private-
/// address guards are <b>not</b> in this path — they are covered by
/// <c>PushHttpClientTests</c>. VAPID keys must be real: the library signs and encrypts
/// before touching HTTP, so placeholder keys throw with zero requests made.
///
/// Assertions read with <c>AsNoTracking()</c> on purpose: tracked entities reflect
/// in-memory changes even before <c>SaveChangesAsync</c>, so a test that reads them would
/// pass even if the service never persisted anything.
/// </summary>
public class PushSendPathTests
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly long FrozenNowUnix = FrozenNow.ToUnixTimeSeconds();

    // ─── Fixture ────────────────────────────────────────────────────────────

    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public AppDbContext Context { get; }
        public PushNotificationService Service { get; }
        public FakePushHandler Handler { get; }
        public FakeTimeProvider Clock { get; }
        public SpyLogger<PushNotificationService> Logger { get; } = new();

        public Fixture(
            IDictionary<string, HttpStatusCode> responses,
            bool gateConcurrentSends = false,
            HttpMessageHandler? handlerOverride = null)
        {
            Connection = new SqliteConnection("DataSource=:memory:");
            Connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(Connection)
                .Options;

            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();

            Clock = new FakeTimeProvider(FrozenNow);
            Handler = new FakePushHandler(responses, gateConcurrentSends);
            Service = BuildService(
                Context, new PushHttpClient(handlerOverride ?? Handler), Clock,
                VapidHelper.GenerateVapidKeys(), logger: Logger);
        }

        public Task SeedUserAndSubscriptionsAsync(params SeedSubscription[] subscriptions) =>
            SeedUserAndSubscriptionsAsync("en", subscriptions);

        public async Task SeedUserAndSubscriptionsAsync(string uiLanguage, SeedSubscription[] subscriptions)
        {
            Context.Users.Add(new User
            {
                Id = 1,
                Guid = Guid.CreateVersion7(),
                Email = "push-target@splitduo.local",
                PasswordHash = "hash",
                FirstName = "Push",
                LastName = "Target",
                Settings = new UserSettings { UiLanguage = uiLanguage },
            });

            var vapid = VapidHelper.GenerateVapidKeys();
            var index = 0;
            foreach (var seed in subscriptions)
            {
                Context.PushSubscriptions.Add(new PushSubscription
                {
                    UserId = 1,
                    Endpoint = seed.Endpoint,
                    // A real P-256 point: the payload is encrypted with it before HTTP.
                    P256dh = vapid.PublicKey,
                    Auth = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16])
                        .TrimEnd('=').Replace('+', '-').Replace('/', '_'),
                    FailureCount = seed.FailureCount,
                    LastUsedAt = seed.LastUsedAt,
                    CreatedAt = FrozenNowUnix - ++index,
                });
            }

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public Task<List<PushSubscription>> ReadSubscriptionsAsync() =>
            Context.PushSubscriptions.AsNoTracking().ToListAsync();

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            Connection.Dispose();
        }
    }

    private sealed record SeedSubscription(
        string Endpoint,
        int FailureCount = 0,
        long? LastUsedAt = null);

    private static PushNotificationService BuildService(
        AppDbContext context, PushHttpClient httpClient, TimeProvider clock, VapidDetails vapid,
        bool configured = true, ILogger<PushNotificationService>? logger = null,
        IStringLocalizer<PushNotificationService>? localizer = null,
        IUnitOfWork? unitOfWork = null) =>
        new(
            logger ?? NullLogger<PushNotificationService>.Instance,
            Options.Create(new PushOptions
            {
                VapidPublicKey = configured ? vapid.PublicKey : "",
                VapidPrivateKey = configured ? vapid.PrivateKey : "",
                Subject = "mailto:tests@splitduo.local",
            }),
            new PushSubscriptionService(
                NullLogger<PushSubscriptionService>.Instance,
                new UnitOfWork(context),
                clock,
                new FakeLocalizer<PushSubscriptionService>()),
            unitOfWork ?? new UnitOfWork(context),
            clock,
            httpClient,
            localizer ?? new FakeLocalizer<PushNotificationService>());

    private sealed class FakeLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, "{0} notification body");
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format("{0} notification body", arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    /// <summary>
    /// Records how the service looks up the body resource: which key, which indexer
    /// overload, the ambient UI culture at lookup time, and the format arguments passed.
    /// Pins both halves of the recipient-language regression.
    /// </summary>
    private sealed class SpyLocalizer : IStringLocalizer<PushNotificationService>
    {
        public sealed record Lookup(string Key, string Overload, string UiCulture, object?[] Arguments);

        public List<Lookup> Lookups { get; } = [];

        public LocalizedString this[string name]
        {
            get
            {
                lock (Lookups)
                    Lookups.Add(new Lookup(name, "single",
                        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, []));
                return new(name, "{0} notification body");
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                lock (Lookups)
                    Lookups.Add(new Lookup(name, "multi",
                        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, arguments));
                return new(name, string.Format("{0} notification body", arguments));
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class SpyLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Entries) Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    /// <summary>
    /// Answers per request URI. Thread-safe because sends run concurrently, and it never
    /// throws. With <c>gateConcurrentSends</c> every request blocks until all expected
    /// requests have arrived, so the sends genuinely overlap instead of completing
    /// synchronously one after another.
    /// </summary>
    private sealed class FakePushHandler(
        IDictionary<string, HttpStatusCode> responses,
        bool gateConcurrentSends = false) : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, HttpStatusCode> _responses = new(responses);
        private readonly List<HttpRequestMessage> _requests = [];
        private readonly Lock _gate = new();
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> RequestUris
        {
            get
            {
                lock (_gate) return _requests.Select(r => r.RequestUri!.ToString()).ToList();
            }
        }

        public int RequestCount
        {
            get
            {
                lock (_gate) return _requests.Count;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();

            lock (_gate)
            {
                _requests.Add(request);
                if (gateConcurrentSends && _requests.Count >= _responses.Count)
                    _allArrived.TrySetResult();
            }

            if (gateConcurrentSends)
                await _allArrived.Task.WaitAsync(cancellationToken);

            var status = _responses.TryGetValue(uri, out var mapped) ? mapped : HttpStatusCode.Created;
            return new HttpResponseMessage(status);
        }
    }

    /// <summary>
    /// Holds each request until its token is cancelled, so the caller-cancellation path is
    /// deterministic rather than a timing race.
    /// </summary>
    private sealed class CancellingHandler : HttpMessageHandler
    {
        public TaskCompletionSource RequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable: the delay was cancelled");
        }
    }

    private static string Endpoint(string name) => $"https://push.example.com/{name}";

    // ─── Dead device: 410 / 404 purge ───────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SendAsync_WhenServiceSaysGone_PurgesTheRow(HttpStatusCode status)
    {
        await using var fixture = new Fixture(new Dictionary<string, HttpStatusCode>
        {
            [Endpoint("dead")] = status,
        });
        await fixture.SeedUserAndSubscriptionsAsync(new SeedSubscription(Endpoint("dead")));

        await fixture.Service.SendAsync(1, Payload(), TestContext.Current.CancellationToken);

        Assert.Empty(await fixture.ReadSubscriptionsAsync());
        Assert.Equal(1, fixture.Handler.RequestCount);
    }

    // ─── Transient failures: count, purge at five ───────────────────────────

    [Fact]
    public async Task SendAsync_FifthConsecutiveFailure_PurgesTheRow()
    {
        await using var fixture = new Fixture(new Dictionary<string, HttpStatusCode>
        {
            [Endpoint("flaky")] = HttpStatusCode.InternalServerError,
        });
        await fixture.SeedUserAndSubscriptionsAsync(new SeedSubscription(Endpoint("flaky")));

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await fixture.Service.SendAsync(1, Payload(), TestContext.Current.CancellationToken);

            var row = Assert.Single(await fixture.ReadSubscriptionsAsync());
            Assert.True(row.FailureCount == attempt,
                $"after attempt {attempt}: expected FailureCount {attempt}, got {row.FailureCount}");
        }

        await fixture.Service.SendAsync(1, Payload(), TestContext.Current.CancellationToken);

        Assert.Empty(await fixture.ReadSubscriptionsAsync());
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)] // 302 — a redirect must count as a failure, not a purge
    public async Task SendAsync_WhenServiceRedirects_CountsAFailureAndKeepsTheRow(HttpStatusCode status)
    {
        await using var fixture = new Fixture(new Dictionary<string, HttpStatusCode>
        {
            [Endpoint("redirect")] = status,
        });
        await fixture.SeedUserAndSubscriptionsAsync(new SeedSubscription(Endpoint("redirect")));

        await fixture.Service.SendAsync(1, Payload(), TestContext.Current.CancellationToken);

        var row = Assert.Single(await fixture.ReadSubscriptionsAsync());
        Assert.Equal(1, row.FailureCount);
    }

    // ─── Recovery: success resets ───────────────────────────────────────────

    [Fact]
    public async Task SendAsync_OnSuccess_ResetsFailuresAndUpdatesLastUsedAt()
    {
        await using var fixture = new Fixture(new Dictionary<string, HttpStatusCode>
        {
            [Endpoint("recovered")] = HttpStatusCode.Created,
        });
        await fixture.SeedUserAndSubscriptionsAsync(
            new SeedSubscription(Endpoint("recovered"), FailureCount: 4, LastUsedAt: FrozenNowUnix - 86_400));

        await fixture.Service.SendAsync(1, Payload(), TestContext.Current.CancellationToken);

        var row = Assert.Single(await fixture.ReadSubscriptionsAsync());
        Assert.Equal(0, row.FailureCount);
        Assert.Equal(FrozenNowUnix, row.LastUsedAt);
    }

    // ─── Concurrency and per-row mapping ────────────────────────────────────

    [Fact]
    public async Task SendAsync_MixedOutcomes_AppliesEachOutcomeToItsOwnRow()
    {
        var healthy = Endpoint("healthy");
        var dead = Endpoint("dead-mixed");
        var flaky = Endpoint("flaky-mixed");

        await using var fixture = new Fixture(new Dictionary<string, HttpStatusCode>
        {
            [healthy] = HttpStatusCode.Created,
            [dead] = HttpStatusCode.Gone,
            [flaky] = HttpStatusCode.InternalServerError,
        }, gateConcurrentSends: true);

        await fixture.SeedUserAndSubscriptionsAsync(
            new SeedSubscription(healthy, FailureCount: 2),
            new SeedSubscription(dead),
            new SeedSubscription(flaky));

        // The gate makes all three requests in flight at once, so this would surface any
        // EF operation left inside the concurrent phase (the pre-fix bug), and the
        // per-endpoint assertions catch an outcome/subscription pairing error.
        await fixture.Service.SendAsync(1, Payload(), TestContext.Current.CancellationToken);

        var rows = await fixture.ReadSubscriptionsAsync();
        Assert.Equal(2, rows.Count);

        var healthyRow = Assert.Single(rows, r => r.Endpoint == healthy);
        Assert.Equal(0, healthyRow.FailureCount); // reset

        Assert.DoesNotContain(rows, r => r.Endpoint == dead);

        var flakyRow = Assert.Single(rows, r => r.Endpoint == flaky);
        Assert.Equal(1, flakyRow.FailureCount);

        Assert.Equal(3, fixture.Handler.RequestCount);
    }

    // ─── Concurrent bookkeeping race is benign ──────────────────────────────

    [Fact]
    public async Task SendAsync_WhenSaveLosesConcurrencyRace_TreatsItAsBenign()
    {
        // A concurrent send (or device removal) can delete the row between our read and
        // our save; EF then throws DbUpdateConcurrencyException even without an explicit
        // token. The database already holds the winning state, so this must not be logged
        // as a catastrophic send failure — pre-fix, the broad catch did exactly that and
        // returned InternalServerError. (The lost update race is silent, not this path.)
        await using var fixture = new Fixture(new Dictionary<string, HttpStatusCode>
        {
            [Endpoint("raced")] = HttpStatusCode.Created,
        });
        await fixture.SeedUserAndSubscriptionsAsync(new SeedSubscription(Endpoint("raced")));

        var uow = Substitute.For<IUnitOfWork>();
        // SendAsync reads Users for the recipient language before sending — an
        // unconfigured substitute returns null and the test would NRE into the outer catch.
        uow.Users.Returns(fixture.Context.Users);
        uow.PushSubscriptions.Returns(fixture.Context.PushSubscriptions);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(
                new DbUpdateConcurrencyException("simulated race")));

        var service = BuildService(
            fixture.Context, new PushHttpClient(fixture.Handler), fixture.Clock,
            VapidHelper.GenerateVapidKeys(), logger: fixture.Logger, unitOfWork: uow);

        var result = await service.SendAsync(1, Payload(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(fixture.Logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Single(fixture.Logger.Entries,
            entry => entry.Level == LogLevel.Debug && entry.Message.Contains("concurrent"));
        uow.Received(1).ClearChangeTracker();

        // The losing save was never retried: the row keeps its pre-send state.
        var row = Assert.Single(await fixture.ReadSubscriptionsAsync());
        Assert.Null(row.LastUsedAt);
    }

    // ─── Negative control ───────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_WhenPushIsNotConfigured_DoesNotSendAndLeavesRowsUntouched()
    {
        await using var fixture = new Fixture(new Dictionary<string, HttpStatusCode>
        {
            [Endpoint("unconfigured")] = HttpStatusCode.InternalServerError,
        });
        await fixture.SeedUserAndSubscriptionsAsync(
            new SeedSubscription(Endpoint("unconfigured"), FailureCount: 0, LastUsedAt: FrozenNowUnix - 100));

        var unconfigured = BuildService(
            fixture.Context, new PushHttpClient(fixture.Handler), fixture.Clock,
            VapidHelper.GenerateVapidKeys(), configured: false);

        await unconfigured.SendAsync(1, Payload(), TestContext.Current.CancellationToken);

        Assert.Equal(0, fixture.Handler.RequestCount);

        // Not just "no requests": if the guard were broken, the send would fail and the
        // failure counter would move even though nothing reached the network.
        var row = Assert.Single(await fixture.ReadSubscriptionsAsync());
        Assert.Equal(0, row.FailureCount);
        Assert.Equal(FrozenNowUnix - 100, row.LastUsedAt);
    }

    // ─── Caller cancellation must not count as a device failure ─────────────

    [Fact]
    public async Task SendAsync_WhenCallerCancelsMidSend_IsSkippedNotFailed()
    {
        // Pre-fix, caller cancellation fell into the generic catch as Failed: the phantom
        // increment stayed in the shared change tracker, and the caller's own tokenless
        // SaveChangesAsync then flushed it — purging a healthy device on the fifth abort.
        // The persistence is invisible here (the service's own save rides the cancelled
        // token), so the classifier is pinned through its other observable effect: the
        // generic catch logs a warning, the Skipped path is silent.
        var cancelling = new CancellingHandler();
        await using var fixture = new Fixture(
            new Dictionary<string, HttpStatusCode>(), handlerOverride: cancelling);

        await fixture.SeedUserAndSubscriptionsAsync(
            new SeedSubscription(Endpoint("cancel-me"), FailureCount: 4, LastUsedAt: FrozenNowUnix - 60));

        using var caller = new CancellationTokenSource();
        var send = fixture.Service.SendAsync(1, Payload(), caller.Token);

        await cancelling.RequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        caller.Cancel();
        await send;

        // Nothing was learned about the endpoint: no warning is logged, and the row keeps
        // its previous failure count instead of moving to 5 and being purged.
        Assert.DoesNotContain(fixture.Logger.Entries,
            entry => entry.Level >= LogLevel.Warning);

        var row = Assert.Single(await fixture.ReadSubscriptionsAsync());
        Assert.Equal(4, row.FailureCount);
        Assert.Equal(FrozenNowUnix - 60, row.LastUsedAt);
    }

    private static PushPayload Payload() =>
        new("Test Group", "PushBodyMemberAdded", ["Someone"], "/groups/abc", "group-abc-member-added-xyz");

    private static StringLocalizer<PushNotificationService> RealLocalizer() =>
        new(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }),
            NullLoggerFactory.Instance));

    [Theory]
    [InlineData("en", "Mario joined the group")]
    [InlineData("it", "Mario si è unito al gruppo")]
    public void ComposeBody_RendersInRecipientLanguageAndRestoresAmbientCulture(
        string language, string expected)
    {
        var other = language == "en" ? "it" : "en";

        // The real resource strings are required: a fake localizer cannot prove that the
        // culture selection reaches the resource resolution itself.
        using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite("DataSource=:memory:").Options);
        var service = BuildService(
            context, new PushHttpClient(new FakePushHandler(new Dictionary<string, HttpStatusCode>())),
            new FakeTimeProvider(FrozenNow), VapidHelper.GenerateVapidKeys(), localizer: RealLocalizer());

        var ambient = new CultureInfo(other);
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = ambient;
        try
        {
            Assert.Equal(expected, service.ComposeBody("PushBodyMemberAdded", language, ["Mario"]));
            // ComposeBody must leave the ambient culture untouched after formatting.
            Assert.Equal(ambient, CultureInfo.CurrentUICulture);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    // ─── Recipient language: the body is rendered in the recipient's language ──

    [Fact]
    public async Task SendAsync_UsesRecipientLanguage_NotCallerCulture()
    {
        var spy = new SpyLocalizer();

        await using var fixture = new Fixture(new Dictionary<string, HttpStatusCode>
        {
            [Endpoint("lang")] = HttpStatusCode.Created,
        });
        await fixture.SeedUserAndSubscriptionsAsync("it", [new SeedSubscription(Endpoint("lang"))]);

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        try
        {
            var service = BuildService(
                fixture.Context, new PushHttpClient(fixture.Handler), fixture.Clock,
                VapidHelper.GenerateVapidKeys(), localizer: spy);

            await service.SendAsync(1, Payload(), TestContext.Current.CancellationToken);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }

        var lookup = Assert.Single(spy.Lookups);
        Assert.Equal("PushBodyMemberAdded", lookup.Key);
        Assert.Equal("single", lookup.Overload);
        Assert.Equal("it", lookup.UiCulture);
        // The broken call shape passed a CultureInfo as the first format argument —
        // the resource was never culture-selected. Pin its absence explicitly.
        Assert.DoesNotContain(spy.Lookups,
            l => l.Arguments.Any(a => a is CultureInfo));
    }
}
