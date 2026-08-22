namespace SahelBundleKeyboard.Core.Automation;

public enum AutomationState
{
    Idle,
    Countdown,
    Running,
    Paused,
    Stopped,
    Completed,
    Error
}

public sealed class AutomationStateChangedEventArgs : EventArgs
{
    public AutomationState Previous { get; }
    public AutomationState Current { get; }

    /// <summary>Optional user-facing (Arabic) status text, e.g. countdown seconds remaining.</summary>
    public string? Message { get; }

    public AutomationStateChangedEventArgs(AutomationState previous, AutomationState current, string? message)
    {
        Previous = previous;
        Current = current;
        Message = message;
    }
}

public sealed class ProgressEventArgs : EventArgs
{
    public int CurrentItem { get; }
    public int TotalItems { get; }
    public int CurrentAction { get; }
    public int TotalActions { get; }

    public ProgressEventArgs(int currentItem, int totalItems, int currentAction, int totalActions)
    {
        CurrentItem = currentItem;
        TotalItems = totalItems;
        CurrentAction = currentAction;
        TotalActions = totalActions;
    }
}
