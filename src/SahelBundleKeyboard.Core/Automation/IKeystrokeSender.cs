namespace SahelBundleKeyboard.Core.Automation;

/// <summary>Low-level keystroke sink. Implementations inject real input on Windows; tests record calls.</summary>
public interface IKeystrokeSender
{
    void TypeText(string text);

    void PressEnter();
}

/// <summary>Cancellable delay abstraction so the engine never blocks and tests control time.</summary>
public interface IDelayService
{
    Task DelayAsync(int milliseconds, CancellationToken cancellationToken);
}
