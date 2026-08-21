using SahelBundleKeyboard.Core.Automation;
using SahelBundleKeyboard.Core.Logging;

namespace SahelBundleKeyboard.Core.Automation
{
    /// <summary>Real time delay service used in production.</summary>
    public sealed class TaskDelayService : IDelayService
    {
        public Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
        {
            if (milliseconds <= 0)
            {
                return cancellationToken.IsCancellationRequested
                    ? Task.FromCanceled(cancellationToken)
                    : Task.CompletedTask;
            }

            return Task.Delay(milliseconds, cancellationToken);
        }
    }
}
