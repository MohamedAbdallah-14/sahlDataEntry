using SahelBundleKeyboard.Core.Automation;
using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Core.Models;

namespace SahelBundleKeyboard.Core.Tests.Automation;

public class AutomationEngineTests
{
    private static Bundle MakeBundle(int items = 2, decimal qty = 1, string name = "حزمة اختبار")
    {
        var bundle = new Bundle { Name = name };
        for (var i = 0; i < items; i++)
        {
            bundle.Items.Add(new BundleItem
            {
                ProductName = "منتج" + (i + 1),
                BaseQuantity = qty,
                Order = i,
                CustomPrice = i == 1 ? 5.5m : null
            });
        }

        return bundle;
    }

    private static async Task WaitForStateAsync(
        AutomationEngine engine,
        AutomationState expected,
        int timeoutMs = 5000)
    {
        var start = DateTime.UtcNow;
        while (engine.State != expected)
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException($"Timed out waiting for state {expected}; current={engine.State}.");
            }

            await Task.Delay(10);
        }
    }

    /// <summary>Paces the engine through every scripted delay until the run task finishes.</summary>
    private static async Task RunToCompletionAsync(AutomationEngine engine, ScriptedDelayService delays, int timeoutMs = 10000)
    {
        var start = DateTime.UtcNow;
        while (true)
        {
            if (engine.CurrentRunTask is { IsCompleted: true })
            {
                return;
            }

            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("The automation run did not finish in time.");
            }

            if (delays.HasPending)
            {
                await delays.AdvanceOneAsync();
            }
            else
            {
                await Task.Delay(5);
            }
        }
    }

    private static async Task<AutomationEngine> StartAndWaitRunningAsync(
        FakeKeystrokeSender sender,
        ScriptedDelayService delays,
        Bundle bundle,
        int count = 1,
        int delayMs = 100)
    {
        var engine = new AutomationEngine(sender, delays, NullLog.Instance);
        var result = await engine.TryStartAsync(new RunRequest(bundle, count, countdownSeconds: 0, delayMilliseconds: delayMs));
        Assert.True(result.Success, result.ErrorMessage);
        await WaitForStateAsync(engine, AutomationState.Running);
        return engine;
    }

    [Fact]
    public async Task HappyPath_ExecutesExactActionSequenceThroughSender()
    {
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = new AutomationEngine(sender, delays, NullLog.Instance);

        // One item: code typed, enter, wait, qty, enter, wait, price typed, enter, wait.
        var bundle = MakeBundle(items: 1);
        bundle.Items[0].ProductCode = "CODE1";
        bundle.Items[0].BaseQuantity = 2;
        bundle.Items[0].CustomPrice = 9.75m;

        var started = await engine.TryStartAsync(new RunRequest(bundle, 3, countdownSeconds: 0, delayMilliseconds: 50));
        Assert.True(started.Success, started.ErrorMessage);

        await RunToCompletionAsync(engine, delays);

        // Search text, Enter, qty, Enter, price, then 5 confirmation Enters.
        Assert.Equal(
        [
            "T:CODE1", "E", "T:6", "E", "T:9.75", "E", "E", "E", "E", "E"
        ], sender.Calls);
        Assert.Equal(AutomationState.Completed, engine.State);
        Assert.Equal([50, 50, 50, 50, 50, 50, 50], delays.RequestedDelays);
    }

    [Fact]
    public async Task Countdown_ZeroStartsImmediately_WithoutCountdownDelays()
    {
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = new AutomationEngine(sender, delays, NullLog.Instance);

        await engine.TryStartAsync(new RunRequest(MakeBundle(), 1, countdownSeconds: 0, delayMilliseconds: 10));
        await RunToCompletionAsync(engine, delays);

        Assert.Equal(AutomationState.Completed, engine.State);
        // Two items -> (2 + 5 confirmation) wait actions each; no countdown waits.
        Assert.Equal(14, delays.RequestedDelays.Count(d => d == 10));
    }

    [Fact]
    public async Task Countdown_ThreeSeconds_CanBeStoppedBeforeRun()
    {
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = new AutomationEngine(sender, delays, NullLog.Instance);
        var states = new List<AutomationState>();
        engine.StateChanged += (_, e) => states.Add(e.Current);

        var started = await engine.TryStartAsync(new RunRequest(MakeBundle(), 1, countdownSeconds: 3, delayMilliseconds: 10));
        Assert.True(started.Success);
        await WaitForStateAsync(engine, AutomationState.Countdown);

        await delays.AdvanceOneAsync(); // one countdown second passes

        engine.Stop();                  // cancel during countdown
        await AwaitRunTaskAsync(engine);

        Assert.Equal(AutomationState.Stopped, engine.State);
        Assert.Empty(sender.Calls);     // nothing was ever typed
        Assert.Contains(AutomationState.Countdown, states);
        Assert.DoesNotContain(AutomationState.Running, states);
    }

    [Fact]
    public async Task Pause_PreservesNextPendingAction_AndResumeContinuesExactly()
    {
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = await StartAndWaitRunningAsync(sender, delays, MakeBundle(items: 2));

        // Deterministic pacing: item1 types its five keystrokes and parks on its final wait.
        await delays.AdvanceToNextParkAsync(); // wait after search text -> Sahel quantity field
        await delays.AdvanceToNextParkAsync(); // wait after quantity   -> Sahel price field

        Assert.Equal(5, sender.Calls.Count); // T,E,T,E,E for the first item are already done
        engine.Pause();

        Assert.Equal(AutomationState.Paused, engine.State);
        var callsAtPause = sender.Calls.Count;

        engine.Resume();
        Assert.Equal(AutomationState.Running, engine.State);
        Assert.Equal(callsAtPause, sender.Calls.Count); // resume itself never types

        await RunToCompletionAsync(engine, delays);

        // Full expected sequence with no duplicated or skipped action
        // (7 Enters per item without price: search+qty+price-confirm+5 confirmations;
        //  item2 adds its typed custom price).
        var expected = new[]
        {
            // item without price: search + qty texts and 7 Enters (price-confirm + 5 confirmations)
            "T:منتج1", "E", "T:1", "E", "E", "E", "E", "E", "E",
            // item with price 5.5 typed between qty-Enter and the confirmation block
            "T:منتج2", "E", "T:1", "E", "T:5.5", "E", "E", "E", "E", "E"
        };
        Assert.Equal(expected, sender.Calls);
        Assert.Equal(AutomationState.Completed, engine.State);
    }

    [Fact]
    public async Task Stop_MidRun_CancelsImmediately_AndResetsToStopped()
    {
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = await StartAndWaitRunningAsync(sender, delays, MakeBundle(items: 5));

        await delays.AdvanceOneAsync();
        await delays.AdvanceOneAsync();
        engine.Stop();

        await AwaitRunTaskAsync(engine);
        Assert.Equal(AutomationState.Stopped, engine.State);

        var callsAfterStop = sender.Calls.Count;
        await Task.Delay(30);
        Assert.Equal(callsAfterStop, sender.Calls.Count); // no further typing after stop
    }

    [Fact]
    public async Task Stop_FromPaused_WakesAndStops()
    {
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = await StartAndWaitRunningAsync(sender, delays, MakeBundle());

        await delays.AdvanceOneAsync();
        engine.Pause();
        Assert.Equal(AutomationState.Paused, engine.State);

        engine.Stop();
        await AwaitRunTaskAsync(engine);
        Assert.Equal(AutomationState.Stopped, engine.State);
    }

    [Fact]
    public async Task DuplicateStart_IsRejectedWhileBusy()
    {
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = new AutomationEngine(sender, delays, NullLog.Instance);

        var first = await engine.TryStartAsync(new RunRequest(MakeBundle(), 1, 0, 100));
        Assert.True(first.Success);

        var second = await engine.TryStartAsync(new RunRequest(MakeBundle(), 1, 0, 100));
        Assert.False(second.Success);
        Assert.False(string.IsNullOrWhiteSpace(second.ErrorMessage));

        await RunToCompletionAsync(engine, delays);

        // After completion a new run is accepted again.
        var third = await engine.TryStartAsync(new RunRequest(MakeBundle(), 1, 0, 100));
        Assert.True(third.Success);
        await RunToCompletionAsync(engine, delays);
        Assert.Equal(AutomationState.Completed, engine.State);
    }

    [Fact]
    public async Task InvalidRequests_AreRejectedWithoutSideEffects()
    {
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = new AutomationEngine(sender, delays, NullLog.Instance);

        var empty = await engine.TryStartAsync(new RunRequest(new Bundle { Name = "فارغة" }, 1, 0, 100));
        Assert.False(empty.Success);
        Assert.Contains("فارغة", empty.ErrorMessage);

        var zeroCount = await engine.TryStartAsync(new RunRequest(MakeBundle(), 0, 0, 100));
        Assert.False(zeroCount.Success);

        Assert.Empty(sender.Calls);
        Assert.Equal(AutomationState.Idle, engine.State);
    }

    [Fact]
    public void Pause_IgnoredWhenNotRunning()
    {
        var engine = new AutomationEngine(new FakeKeystrokeSender(), new ScriptedDelayService(), NullLog.Instance);
        engine.Pause();
        Assert.Equal(AutomationState.Idle, engine.State);
        engine.Resume();
        Assert.Equal(AutomationState.Idle, engine.State);
    }

    [Fact]
    public async Task SenderFailure_TransitionsToErrorState()
    {
        var sender = new FakeKeystrokeSender { TypeTextGate = text => text == "boom" };
        var bundle = MakeBundle(items: 1);
        bundle.Items[0].ProductName = "boom";

        var delays = new ScriptedDelayService();
        var engine = new AutomationEngine(sender, delays, NullLog.Instance);

        await engine.TryStartAsync(new RunRequest(bundle, 1, 0, 10));
        await RunToCompletionAsync(engine, delays);

        Assert.Equal(AutomationState.Error, engine.State);
        Assert.Empty(sender.Calls); // the throwing call never recorded a keystroke
    }

    [Fact]
    public async Task Progress_ReportsItemBoundaries()
    {
        var progress = new List<(int Current, int Total)>();
        var sender = new FakeKeystrokeSender();
        var delays = new ScriptedDelayService();
        var engine = new AutomationEngine(sender, delays, NullLog.Instance);
        engine.ProgressChanged += (_, e) => progress.Add((e.CurrentItem, e.TotalItems));

        await engine.TryStartAsync(new RunRequest(MakeBundle(items: 3), 1, 0, 20));
        await RunToCompletionAsync(engine, delays);

        Assert.Equal([(1, 3), (2, 3), (3, 3)], progress);
    }

    private static async Task AwaitRunTaskAsync(AutomationEngine engine)
    {
        Assert.NotNull(engine.CurrentRunTask);
        await engine.CurrentRunTask!;
    }
}
