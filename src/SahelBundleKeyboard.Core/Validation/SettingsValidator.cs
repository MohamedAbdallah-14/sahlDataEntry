using SahelBundleKeyboard.Core.Hotkeys;
using SahelBundleKeyboard.Core.Models;

namespace SahelBundleKeyboard.Core.Validation;

public static class SettingsValidator
{
    public static ValidationResult Validate(AppSettings settings)
    {
        var result = new ValidationResult();

        if (settings.DelayMilliseconds < SettingLimits.MinDelayMilliseconds ||
            settings.DelayMilliseconds > SettingLimits.MaxDelayMilliseconds)
        {
            result.AddError(nameof(settings.DelayMilliseconds),
                $"التأخير يجب أن يكون بين {SettingLimits.MinDelayMilliseconds} و {SettingLimits.MaxDelayMilliseconds} مللي ثانية.");
        }

        if (settings.CountdownSeconds < SettingLimits.MinCountdownSeconds ||
            settings.CountdownSeconds > SettingLimits.MaxCountdownSeconds)
        {
            result.AddError(nameof(settings.CountdownSeconds),
                $"العد التنازلي يجب أن يكون بين {SettingLimits.MinCountdownSeconds} و {SettingLimits.MaxCountdownSeconds} ثانية.");
        }

        ValidateShortcut(settings.StartShortcut, "Start", "بدء التشغيل", result);
        ValidateShortcut(settings.PauseResumeShortcut, "PauseResume", "إيقاف مؤقت/استئناف", result);
        ValidateShortcut(settings.StopShortcut, "Stop", "إيقاف نهائي", result);

        var parsed = new[]
        {
            (Name: "Start", ArabicName: "بدء التشغيل", Combo: HotkeyParser.TryParse(settings.StartShortcut)),
            (Name: "PauseResume", ArabicName: "إيقاف مؤقت/استئناف", Combo: HotkeyParser.TryParse(settings.PauseResumeShortcut)),
            (Name: "Stop", ArabicName: "إيقاف نهائي", Combo: HotkeyParser.TryParse(settings.StopShortcut))
        };

        for (var i = 0; i < parsed.Length; i++)
        {
            for (var j = i + 1; j < parsed.Length; j++)
            {
                var a = parsed[i];
                var b = parsed[j];
                if (a.Combo is not null && b.Combo is not null && a.Combo.Equals(b.Combo))
                {
                    result.AddError("Shortcuts",
                        $"اختصار \"{a.ArabicName}\" واختصار \"{b.ArabicName}\" متطابقان. يجب أن يكون لكل وظيفة اختصار مختلف.");
                }
            }
        }

        return result;
    }

    private static void ValidateShortcut(string shortcut, string field, string arabicName, ValidationResult result)
    {
        if (!HotkeyParser.TryValidate(shortcut, out var error))
        {
            result.AddError(field, $"اختصار \"{arabicName}\" غير صالح: {error}");
        }
    }
}
