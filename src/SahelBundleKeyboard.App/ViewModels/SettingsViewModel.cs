using System.IO;
using Microsoft.Win32;
using SahelBundleKeyboard.App.Infrastructure;
using SahelBundleKeyboard.App.Services;
using SahelBundleKeyboard.Core.Hotkeys;
using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Infrastructure.Backup;
using SahelBundleKeyboard.Infrastructure.Persistence;
using SahelBundleKeyboard.Windows.Hotkeys;

namespace SahelBundleKeyboard.App.ViewModels;

/// <summary>Settings tab: delay, countdown, shortcuts, backup, help.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppDataService _data;
    private readonly GlobalHotkeyManager _hotkeys;
    private readonly BackupService _backup;
    private readonly Action _reapplyShortcutsAndReport;

    private string _delayText;
    private int _countdownIndex;

    public SettingsViewModel(
        AppDataService data,
        GlobalHotkeyManager? hotkeys,
        BackupService backup,
        Action reapplyShortcutsAndReport)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        _backup = backup ?? throw new ArgumentNullException(nameof(backup));
        _reapplyShortcutsAndReport = reapplyShortcutsAndReport;

        var settings = data.Document.Settings;
        _delayText = settings.DelayMilliseconds.ToString();
        _countdownIndex = settings.CountdownSeconds; // 0..3 maps directly to combo index
        StartShortcut = settings.StartShortcut;
        StopShortcut = settings.StopShortcut;

        ExportBackupCommand = new RelayCommand(ExportBackup);
        ImportBackupCommand = new RelayCommand(ImportBackup);
    }

    public RelayCommand ExportBackupCommand { get; }
    public RelayCommand ImportBackupCommand { get; }

    public string DelayText
    {
        get => _delayText;
        set
        {
            if (!SetProperty(ref _delayText, value))
            {
                return;
            }

            if (QuantityFormatterTryParseInt(value, out var delay) &&
                delay is >= SettingLimits.MinDelayMilliseconds and <= SettingLimits.MaxDelayMilliseconds)
            {
                _data.Document.Settings.DelayMilliseconds = delay;
                _data.Save();
            }
        }
    }

    public System.Collections.Generic.IReadOnlyList<string> CountdownOptions { get; } = ["بدون عد تنازلي", "ثانية واحدة", "ثانيتان", "3 ثوان"];

    public int CountdownIndex
    {
        get => _countdownIndex;
        set
        {
            if (SetProperty(ref _countdownIndex, value))
            {
                _data.Document.Settings.CountdownSeconds = value;
                _data.Save();
            }
        }
    }

    public string StartShortcut
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && field.Length > 0)
            {
                UpdateShortcut(s => s.StartShortcut = field);
            }
        }
    } = string.Empty;

    public string StopShortcut
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && field.Length > 0)
            {
                UpdateShortcut(s => s.StopShortcut = field);
            }
        }
    } = string.Empty;

    public string HelpText =>
        "طريقة الاستخدام:\n" +
        "1. افتح برنامج سهل واختر الحزمة وعدد الحزم من هذا البرنامج.\n" +
        "2. اضغط في حقل البحث عن المنتجات داخل سهل ليصبح هو النافذة النشطة.\n" +
        "3. ابدأ بالاختصار العام أو بزر \"ابدأ\" في وحدة التحكم العائمة.\n\n" +
        UiText.StopIrreversibleNote + "\n\n" +
        "إذا كان سهل يعمل بصلاحيات المسؤول (Run as administrator)، شغّل هذا البرنامج أيضاً بصلاحيات المسؤول " +
        "بالزر الأيمن ثم \"تشغيل كمسؤول\"، وإلا فلن تصل ضغطات المفاتيح إلى سهل.\n\n" +
        "ضع مجلد البرنامج في مكان قابل للكتابة (مثل المستندات أو سطح المكتب) وليس داخل Program Files.";

    private void UpdateShortcut(Action<SahelBundleKeyboard.Core.Models.AppSettings> apply)
    {
        var previousStart = _data.Document.Settings.StartShortcut;
        var previousStop = _data.Document.Settings.StopShortcut;

        apply(_data.Document.Settings);

        var validation = SahelBundleKeyboard.Core.Validation.SettingsValidator.Validate(_data.Document.Settings);
        if (!validation.IsValid)
        {
            UiText.ShowError(validation.ToAggregatedMessage());
            // Keep the last valid configuration.
            _data.Document.Settings.StartShortcut = previousStart;
            _data.Document.Settings.StopShortcut = previousStop;

            StartShortcut = previousStart;
            StopShortcut = previousStop;
            return;
        }

        _data.Save();
        _reapplyShortcutsAndReport();
    }

    private void ExportBackup()
    {
        var dialog = new SaveFileDialog
        {
            Title = "تصدير نسخة احتياطية كاملة",
            FileName = $"نسخة-احتياطية-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Filter = "JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _backup.Export(_data.Document, dialog.FileName);
            UiText.ShowInfo("تم تصدير النسخة الاحتياطية بنجاح.");
        }
        catch (PersistenceException ex)
        {
            UiText.ShowError(ex.UserMessage);
        }
    }

    private void ImportBackup()
    {
        var dialog = new OpenFileDialog
        {
            Title = "استيراد نسخة احتياطية",
            Filter = "JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        BackupPreview preview;
        try
        {
            preview = _backup.ReadPreview(dialog.FileName);
        }
        catch (PersistenceException ex)
        {
            UiText.ShowError(ex.UserMessage);
            return;
        }

        var summary =
            $"الحزم: {preview.BundleCount}\n" +
            $"الأصناف: {preview.ItemCount}\n" +
            $"التأخير: {preview.DelayMilliseconds} مللي ثانية\n" +
            $"العد التنازلي: {preview.CountdownSeconds} ثانية/ثوان\n" +
            $"الاختصارات: {preview.StartShortcut} / {preview.StopShortcut}\n\n" +
            "سيتم استبدال جميع البيانات الحالية (مع إنشاء نسخة أمان تلقائية). هل تريد المتابعة؟";

        if (!UiText.Confirm(summary))
        {
            return;
        }

        try
        {
            var imported = _backup.ApplyImport(preview);
            _data.ReplaceDocument(imported);

            DelayText = imported.Settings.DelayMilliseconds.ToString();
            CountdownIndex = imported.Settings.CountdownSeconds;
            StartShortcut = imported.Settings.StartShortcut;
            StopShortcut = imported.Settings.StopShortcut;

            _reapplyShortcutsAndReport();
            UiText.ShowInfo("تم استيراد النسخة الاحتياطية بنجاح.");
        }
        catch (PersistenceException ex)
        {
            UiText.ShowError(ex.UserMessage);
        }
    }

    private static bool QuantityFormatterTryParseInt(string text, out int value)
    {
        var normalized = Core.Sequencing.QuantityFormatter.NormalizeDigits(text.Trim());
        return int.TryParse(normalized, out value);
    }

    public void LoadFromDocument()
    {
        var settings = _data.Document.Settings;
        DelayText = settings.DelayMilliseconds.ToString();
        CountdownIndex = settings.CountdownSeconds;
        StartShortcut = settings.StartShortcut;
        StopShortcut = settings.StopShortcut;
    }
}
