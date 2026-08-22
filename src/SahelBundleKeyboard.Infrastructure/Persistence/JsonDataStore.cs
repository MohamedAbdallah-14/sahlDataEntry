using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SahelBundleKeyboard.Core.Logging;

namespace SahelBundleKeyboard.Infrastructure.Persistence;

/// <summary>
/// JSON persistence with atomic saves (temp file + replace) and safe handling of
/// corrupt/unsupported files: the original bytes are never overwritten — the broken
/// file is moved aside under a timestamped name and a fresh document is returned.
/// </summary>
public sealed class JsonDataStore
{
    private const string LogSource = "Persistence";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly DataPaths _paths;
    private readonly ILog _log;

    public JsonDataStore(DataPaths paths, ILog log)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Loads the document, creating defaults on first run and quarantining corrupt files.</summary>
    public LoadResult Load()
    {
        _paths.EnsureCreated();

        if (!File.Exists(_paths.DataFile))
        {
            _log.Info(LogSource, "No data file found; starting fresh (first run).");
            return new LoadResult(AppDataDocument.CreateDefault(), false, null);
        }

        string json;
        try
        {
            json = File.ReadAllText(_paths.DataFile, new UTF8Encoding(false));
        }
        catch (IOException ex)
        {
            throw new PersistenceException(
                "تعذر قراءة ملف البيانات. تأكد من أن المجلد قابل للكتابة وأن الملف غير مستخدم من برنامج آخر.",
                ex.Message, ex);
        }

        AppDataDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<AppDataDocument>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            return QuarantineAndStartFresh($"تعذر تحليل ملف البيانات (JSON غير صالح): {ex.Message}");
        }

        if (document is null)
        {
            return QuarantineAndStartFresh("ملف البيانات فارغ أو غير صالح.");
        }

        if (document.SchemaVersion > AppDataDocument.CurrentSchemaVersion)
        {
            return QuarantineAndStartFresh(
                $"إصدار ملف البيانات ({document.SchemaVersion}) أحدث من إصدار هذا البرنامج ({AppDataDocument.CurrentSchemaVersion}).");
        }

        // Future migrations for older versions plug in here.
        document.SchemaVersion = AppDataDocument.CurrentSchemaVersion;

        _log.Info(LogSource, $"Data loaded: bundles={document.Bundles.Count}.");
        return new LoadResult(document, false, null);
    }

    private LoadResult QuarantineAndStartFresh(string warning)
    {
        var quarantinePath = _paths.DataFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        try
        {
            if (File.Exists(_paths.DataFile))
            {
                File.Move(_paths.DataFile, quarantinePath);
                _log.Warn(LogSource, $"Corrupt data file moved to {quarantinePath}.");
                warning += $" تم نقل الملف التالف إلى: {Path.GetFileName(quarantinePath)}";
            }
        }
        catch (IOException ex)
        {
            _log.Error(LogSource, "Failed to quarantine corrupt data file.", ex);
            warning += " (تعذر نقل الملف التالف؛ سيبدأ البرنامج ببيانات فارغة دون الكتابة فوق الملف الأصلي)";
        }

        _log.Warn(LogSource, "Starting with empty in-memory data after corruption.");
        return new LoadResult(AppDataDocument.CreateDefault(), true, warning);
    }

    /// <summary>Saves atomically: write temp file, flush, then replace the target.</summary>
    public void Save(AppDataDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _paths.EnsureCreated();

        var tempFile = _paths.DataFile + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(document, SerializerOptions);
            WriteAllTextFlushed(tempFile, json);

            if (File.Exists(_paths.DataFile))
            {
                File.Replace(tempFile, _paths.DataFile, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempFile, _paths.DataFile);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempFile);
            throw new PersistenceException(
                "فشل حفظ البيانات. تأكد من أن مجلد البرنامج قابل للكتابة (لا تضعه داخل Program Files مثلاً).",
                ex.Message, ex);
        }
    }

    private static void WriteAllTextFlushed(string path, string contents)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contents);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // best effort cleanup
        }
    }
}
