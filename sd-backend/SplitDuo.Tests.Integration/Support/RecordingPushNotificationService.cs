using SplitDuo.Core.Common;
using SplitDuo.Core.Services;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// Records what the application asked to be pushed, instead of sending it.
///
/// It observes <b>selection</b>, not delivery: the real sender also skips deleted users
/// and users without subscriptions. That distinction is deliberate — the triggers under
/// test are responsible for choosing recipients, not for delivering.
///
/// It never throws: every production caller swallows sender exceptions
/// (`catch { }` / log-only), so a throwing fake would turn a broken trigger into a green,
/// empty test.
/// </summary>
public sealed class RecordingPushNotificationService : IPushNotificationService
{
    private readonly List<(int UserId, PushPayload Payload)> _sends = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<(int UserId, PushPayload Payload)> Sends
    {
        get
        {
            lock (_gate) return _sends.ToList();
        }
    }

    /// <summary>
    /// Test-owned hook awaited at the start of every send, before recording.
    /// Lets a test observe database state at the exact moment the push fires.
    /// The hook must not throw (the recorder itself stays never-throwing);
    /// cleared by <see cref="Reset"/> so the singleton cannot leak it across tests.
    /// </summary>
    public Func<int, PushPayload, Task>? OnSend { get; set; }

    public async Task<Result> SendAsync(int userId, PushPayload payload, CancellationToken cancellationToken = default)
    {
        if (OnSend is { } hook)
            await hook(userId, payload);

        lock (_gate) _sends.Add((userId, payload));
        return Result.Success();
    }

    public void Reset()
    {
        lock (_gate) _sends.Clear();
        OnSend = null;
    }
}
