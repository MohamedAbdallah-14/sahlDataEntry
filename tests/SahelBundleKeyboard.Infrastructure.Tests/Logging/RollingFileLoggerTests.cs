using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Infrastructure.Logging;

namespace SahelBundleKeyboard.Infrastructure.Tests.Logging;

public class RollingFileLoggerTests
{
    [Fact]
    public void WritesLines_WithLevelAndSource_AndNeverTheExceptionSecrets()
    {
        var folder = Path.Combine(Path.GetTempPath(), "sbk-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new RollingFileLogger(folder);
            logger.Info("Src", "hello");
            logger.Warn("Src", "careful");
            logger.Error("Src", "boom", new InvalidOperationException("detail"));

            var logFile = Directory.GetFiles(folder, "app-*.log").Single();
            var content = File.ReadAllText(logFile);

            Assert.Contains("[INFO] [Src] hello", content);
            Assert.Contains("[WARN] [Src] careful", content);
            Assert.Contains("[ERROR] [Src] boom", content);
            Assert.Contains("InvalidOperationException: detail", content);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Rotation_CreatesSuffixedFile_WhenSizeCapReached()
    {
        var folder = Path.Combine(Path.GetTempPath(), "sbk-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new RollingFileLogger(folder);

            // Write enough to exceed the 512 KB cap in one call.
            var bigMessage = new string('x', 600 * 1024);
            logger.Info("Src", bigMessage);
            logger.Info("Src", "after-rotation");

            Assert.True(File.Exists(Path.Combine(folder, "app-" + DateTime.Now.ToString("yyyyMMdd") + ".log.1")));
            var current = File.ReadAllText(Path.Combine(folder, "app-" + DateTime.Now.ToString("yyyyMMdd") + ".log"));
            Assert.DoesNotContain(bigMessage[..100], current); // old content rotated away
            Assert.Contains("after-rotation", current);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

}

