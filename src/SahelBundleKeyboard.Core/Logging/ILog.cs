namespace SahelBundleKeyboard.Core.Logging;

/// <summary>
/// Minimal logging abstraction. Implementations must be bounded (rolling) and must never
/// persist typed product text or run history — only counts, states and technical errors.
/// </summary>
public interface ILog
{
    void Info(string source, string message);

    void Warn(string source, string message);

    void Error(string source, string message, Exception? exception = null);
}
