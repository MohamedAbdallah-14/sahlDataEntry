using SahelBundleKeyboard.Core.Automation;

namespace SahelBundleKeyboard.Core.Tests.Automation;

/// <summary>Records every keystroke call; optionally throws on demand.</summary>
public sealed class FakeKeystrokeSender : IKeystrokeSender
{
    private readonly List<string> _calls = [];
    private readonly object _lock = new();

    public Func<string, bool>? TypeTextGate { get; set; }

    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    public void TypeText(string text)
    {
        if (TypeTextGate?.Invoke(text) == true)
        {
            throw new InvalidOperationException("فشل حقن لوحة المفاتيح (اختبار).");
        }

        lock (_lock)
        {
            _calls.Add("T:" + text);
        }
    }

    public void PressEnter()
    {
        lock (_lock)
        {
            _calls.Add("E");
        }
    }
}

/// <summary>
/// Delay service whose delays are completed manually by the test.
/// All methods wait for the engine's pool thread to actually reach a delay,
/// so tests are deterministic instead of racy.
/// </summary>
public sealed class ScriptedDelayService : IDelayService
{
    private readonly Queue<TaskCompletionSource<bool>> _pending = new();
    private readonly object _lock = new();

    public List<int> RequestedDelays { get; } = [];

    public bool HasPending
    {
        get
        {
            lock (_lock)
            {
                return _pending.Count > 0;
            }
        }
    }

    public async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            RequestedDelays.Add(milliseconds);
            _pending.Enqueue(tcs);
        }

        if (cancellationToken.CanBeCanceled)
        {
            await using var registration = cancellationToken.Register(
                static state => ((TaskCompletionSource<bool>)state!).TrySetResult(false),
                tcs);

            var result = await tcs.Task;
            if (!result)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
        else
        {
            await tcs.Task;
        }
    }

    /// <summary>Waits until the engine has one pending delay, then completes it.</summary>
    public async Task AdvanceOneAsync(int timeoutMs = 5000)
    {
        var tcs = await TakePendingAsync(timeoutMs);
        tcs.TrySetResult(true);
    }

    /// <summary>
    /// Advances one delay, then waits until the engine reached its next delay.
    /// After this call the engine is deterministically parked between actions.
    /// </summary>
    public async Task AdvanceToNextParkAsync(int timeoutMs = 5000)
    {
        await AdvanceOneAsync(timeoutMs);
        await WaitUntilPendingAsync(timeoutMs);
    }

    /// <summary>Waits until at least one delay is pending (engine is parked).</summary>
    public async Task WaitUntilPendingAsync(int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond + timeoutMs;
        while (!HasPending)
        {
            if (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond > deadline)
            {
                throw new TimeoutException("Timed out waiting for the engine to park on a delay.");
            }

            await Task.Delay(5);
        }
    }

    private async Task<TaskCompletionSource<bool>> TakePendingAsync(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond + timeoutMs;
        while (true)
        {
            lock (_lock)
            {
                if (_pending.Count > 0)
                {
                    return _pending.Dequeue();
                }
            }

            if (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond > deadline)
            {
                throw new TimeoutException("Timed out waiting for the engine to request a delay.");
            }

            await Task.Delay(5);
        }
    }
}
