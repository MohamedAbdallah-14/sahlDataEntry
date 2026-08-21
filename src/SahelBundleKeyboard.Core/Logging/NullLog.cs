namespace SahelBundleKeyboard.Core.Logging;

/// <summary>No-op logger for tests and design-time composition.</summary>
public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    public void Info(string source, string message)
    {
    }

    public void Warn(string source, string message)
    {
    }

    public void Error(string source, string message, Exception? exception = null)
    {
    }
}
