namespace SahelBundleKeyboard.Core.Models;

public static class SettingLimits
{
    public const int MinDelayMilliseconds = 0;
    public const int MaxDelayMilliseconds = 5000;
    public const int MinCountdownSeconds = 0;
    public const int MaxCountdownSeconds = 3;
    public const int MinBundleCount = 1;
}

public sealed class AppSettings
{
    public const int DefaultDelayMilliseconds = 120;
    public const int DefaultCountdownSeconds = 2;

    /// <summary>Global delay in ms applied between automation actions (0..5000).</summary>
    public int DelayMilliseconds { get; set; } = DefaultDelayMilliseconds;

    /// <summary>Countdown before start. Restricted to 0, 1, 2 or 3 seconds.</summary>
    public int CountdownSeconds { get; set; } = DefaultCountdownSeconds;

    public string StartShortcut { get; set; } = "Ctrl+Alt+G";
    public string PauseResumeShortcut { get; set; } = "Ctrl+Alt+P";
    public string StopShortcut { get; set; } = "Ctrl+Alt+S";

    /// <summary>Last selected bundle id (persisted when practical).</summary>
    public string? LastSelectedBundleId { get; set; }

    /// <summary>Last whole-number bundle count (persisted when practical).</summary>
    public int LastBundleCount { get; set; } = 1;
}
