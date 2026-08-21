namespace SahelBundleKeyboard.Core.Automation;

/// <summary>
/// Async gate used to pause the engine between individual input actions.
/// Closing the gate blocks waiters; opening releases all of them.
/// </summary>
public sealed class AsyncGate
{
    private readonly object _lock = new();
    private TaskCompletionSource<bool> _tcs = CreateCompleted();
    private bool _closed;

    public bool IsClosed
    {
        get
        {
            lock (_lock)
            {
                return _closed;
            }
        }
    }

    /// <summary>Returns a task that completes when the gate opens. Never blocks synchronously.</summary>
    public Task WaitAsync(CancellationToken cancellationToken)
    {
        Task task;
        lock (_lock)
        {
            task = _tcs.Task;
        }

        return task.WaitAsync(cancellationToken);
    }

    public void Close()
    {
        lock (_lock)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Open()
    {
        lock (_lock)
        {
            if (!_closed)
            {
                return;
            }

            _closed = false;
            _tcs.TrySetResult(true);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _closed = false;
            _tcs.TrySetResult(true);
            _tcs = CreateCompleted();
        }
    }

    private static TaskCompletionSource<bool> CreateCompleted()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.TrySetResult(true);
        return tcs;
    }
}
