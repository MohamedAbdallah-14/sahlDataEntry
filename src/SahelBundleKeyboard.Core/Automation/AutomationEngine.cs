using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Core.Sequencing;

namespace SahelBundleKeyboard.Core.Automation;

/// <summary>A validated request to run one bundle automation pass.</summary>
public sealed class RunRequest
{
    public Bundle Bundle { get; }
    public int BundleCount { get; }
    public int CountdownSeconds { get; }
    public int DelayMilliseconds { get; }

    public RunRequest(Bundle bundle, int bundleCount, int countdownSeconds, int delayMilliseconds)
    {
        Bundle = bundle;
        BundleCount = bundleCount;
        CountdownSeconds = countdownSeconds;
        DelayMilliseconds = delayMilliseconds;
    }
}

public sealed record StartRunResult(bool Success, string? ErrorMessage = null)
{
    public static StartRunResult Ok() => new(true, null);
    public static StartRunResult Fail(string message) => new(false, message);
}

/// <summary>
/// Cancellation-aware automation state machine.
///
/// Guarantees:
/// - only one run at a time (duplicate starts are rejected),
/// - pause preserves the exact next pending action (checked between every key action),
/// - stop cancels immediately, including during the countdown,
/// - the UI thread is never blocked; all waiting is asynchronous.
/// </summary>
public sealed class AutomationEngine
{
    private const string LogSource = "Engine";

    private readonly IKeystrokeSender _sender;
    private readonly IDelayService _delayService;
    private readonly ILog _log;
    private readonly AsyncGate _pauseGate = new();
    private readonly SemaphoreSlim _startLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private volatile AutomationState _state = AutomationState.Idle;

    public AutomationEngine(IKeystrokeSender sender, IDelayService delayService, ILog log)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _delayService = delayService ?? throw new ArgumentNullException(nameof(delayService));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public AutomationState State => _state;

    /// <summary>True while a countdown or a run is active.</summary>
    public bool IsBusy =>
        _state is AutomationState.Countdown or AutomationState.Running or AutomationState.Paused;

    /// <summary>The active/completed run task, or null before the first run. Used by hosts and tests.</summary>
    public Task? CurrentRunTask => _runTask;

    public event EventHandler<AutomationStateChangedEventArgs>? StateChanged;

    public event EventHandler<ProgressEventArgs>? ProgressChanged;

    /// <summary>Attempts to start a run. Returns a failure result when busy, invalid, or empty.</summary>
    public async Task<StartRunResult> TryStartAsync(RunRequest request)
    {
        await _startLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsBusy || (_runTask is not null && !_runTask.IsCompleted))
            {
                return StartRunResult.Fail("هناك عملية تشغيل جارية بالفعل. أوقفها أولاً.");
            }

            var validation = Validation.BundleValidator.ValidateForRun(request.Bundle, request.BundleCount);
            if (!validation.IsValid)
            {
                _log.Warn(LogSource, $"Start rejected: bundle validation failed with {validation.Errors.Count} error(s).");
                return StartRunResult.Fail(validation.ToAggregatedMessage());
            }

            var delayMs = Math.Clamp(
                request.DelayMilliseconds,
                SettingLimits.MinDelayMilliseconds,
                SettingLimits.MaxDelayMilliseconds);
            var countdownSeconds = Math.Clamp(
                request.CountdownSeconds,
                SettingLimits.MinCountdownSeconds,
                SettingLimits.MaxCountdownSeconds);

            var actions = SequenceBuilder.Build(request.Bundle, request.BundleCount, delayMs);

            _pauseGate.Reset();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var totalItems = actions.Count == 0 ? 0 : actions.Max(a => a.ItemIndex) + 1;
            SetState(AutomationState.Countdown,
                countdownSeconds > 0 ? $"سيبدأ الإدخال خلال {countdownSeconds} ثانية…" : null);

            // Force execution off the caller's (UI) thread: with countdown=0 and delay=0
            // every await would otherwise complete synchronously and block the UI.
            var cts = _cts;
            _runTask = Task.Run(() => RunAsync(actions, totalItems, countdownSeconds, cts.Token));

            return StartRunResult.Ok();
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <summary>Pauses between input actions. The exact next pending action is preserved.</summary>
    public void Pause()
    {
        if (_state != AutomationState.Running)
        {
            return;
        }

        _pauseGate.Close();
        if (_state == AutomationState.Running)
        {
            SetState(AutomationState.Paused, "تم الإيقاف المؤقت. سيُستأنف من نفس الخطوة بالضبط.");
        }
    }

    public void Resume()
    {
        if (_state != AutomationState.Paused)
        {
            return;
        }

        SetState(AutomationState.Running, null);
        _pauseGate.Open();
    }

    /// <summary>Cancels the countdown or the running sequence immediately and resets to Stopped.</summary>
    public void Stop()
    {
        if (!IsBusy)
        {
            return;
        }

        // Open the gate so a paused engine wakes up into cancellation instead of hanging.
        _pauseGate.Open();
        _cts?.Cancel();

        if (_state != AutomationState.Stopped)
        {
            _log.Info(LogSource, "Stop requested by user.");
        }
    }

    private async Task RunAsync(IReadOnlyList<InputAction> actions, int totalItems, int countdownSeconds, CancellationToken ct)
    {
        try
        {
            for (var remaining = countdownSeconds; remaining > 0; remaining--)
            {
                SetState(AutomationState.Countdown, $"سيبدأ الإدخال خلال {remaining} ثانية…");
                await _delayService.DelayAsync(1000, ct).ConfigureAwait(false);
            }

            SetState(AutomationState.Running, null);

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];

                // Pause is honored between individual key actions, not only between products.
                await _pauseGate.WaitAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();

                switch (action)
                {
                    case TypeTextAction text:
                        _sender.TypeText(text.Text);
                        break;

                    case PressEnterAction:
                        _sender.PressEnter();
                        break;

                    case WaitAction wait:
                        await _delayService.DelayAsync(wait.DelayMilliseconds, ct).ConfigureAwait(false);
                        break;

                    default:
                        throw new InvalidOperationException($"نوع إجراء غير معروف: {action.GetType().Name}");
                }

                var isLastActionOfItem = i + 1 >= actions.Count ||
                                         actions[i + 1].ItemIndex != action.ItemIndex;
                if (isLastActionOfItem)
                {
                    ProgressChanged?.Invoke(this,
                        new ProgressEventArgs(action.ItemIndex + 1, totalItems, i + 1, actions.Count));
                }
            }

            SetState(AutomationState.Completed, "تم إدخال الحزمة بالكامل بنجاح.");
            _log.Info(LogSource, $"Run completed: items={totalItems}, actions={actions.Count}.");
        }
        catch (OperationCanceledException)
        {
            SetState(AutomationState.Stopped, "تم الإيقاف. النص الذي تم إدخاله قبل الإيقاف لا يمكن التراجع عنه تلقائياً.");
            _log.Info(LogSource, "Run stopped by cancellation.");
        }
        catch (Exception ex)
        {
            SetState(AutomationState.Error, "حدث خطأ أثناء إدخال البيانات. راجع ملف السجل للتفاصيل التقنية.");
            _log.Error(LogSource, "Run failed.", ex);
        }
    }

    private void SetState(AutomationState current, string? message)
    {
        var previous = _state;
        _state = current;
        StateChanged?.Invoke(this, new AutomationStateChangedEventArgs(previous, current, message));
    }
}
