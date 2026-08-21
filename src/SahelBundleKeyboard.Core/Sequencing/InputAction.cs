namespace SahelBundleKeyboard.Core.Sequencing;

/// <summary>A logical automation action. The engine executes these through an injectable sender.</summary>
public abstract record InputAction
{
    /// <summary>Zero-based index of the bundle item this action belongs to (for progress reporting).</summary>
    public int ItemIndex { get; init; }
}

/// <summary>Type a text string as Unicode keystrokes (product code/name/quantity/price).</summary>
public sealed record TypeTextAction(string Text) : InputAction;

/// <summary>Press the Enter key.</summary>
public sealed record PressEnterAction() : InputAction;

/// <summary>Wait for the configured global delay so Sahel can advance to its next field.</summary>
public sealed record WaitAction(int DelayMilliseconds) : InputAction;
