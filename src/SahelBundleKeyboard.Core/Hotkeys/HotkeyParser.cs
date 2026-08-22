using System.Globalization;

namespace SahelBundleKeyboard.Core.Hotkeys;

/// <summary>
/// Parses shortcut strings like "Ctrl+Alt+G" into a <see cref="HotkeyCombo"/>.
/// Rules:
/// - at least one modifier and exactly one key,
/// - must contain Ctrl or Alt so normal typing shortcuts are never hijacked,
/// - F12 is rejected (reserved by Windows for the debugger).
/// </summary>
public static class HotkeyParser
{
    public const string ReservedKeyError = "لا يمكن استخدام مفتاح F12 لأن ويندوز يحجزه لتصحيح الأخطاء.";

    private static readonly Dictionary<string, HotkeyModifiers> ModifierTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = HotkeyModifiers.Ctrl,
        ["Control"] = HotkeyModifiers.Ctrl,
        ["Alt"] = HotkeyModifiers.Alt,
        ["Shift"] = HotkeyModifiers.Shift,
        ["Win"] = HotkeyModifiers.Win,
        ["Windows"] = HotkeyModifiers.Win
    };

    private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["NumPad0"] = 0x60,
        ["NumPad1"] = 0x61,
        ["NumPad2"] = 0x62,
        ["NumPad3"] = 0x63,
        ["NumPad4"] = 0x64,
        ["NumPad5"] = 0x65,
        ["NumPad6"] = 0x66,
        ["NumPad7"] = 0x67,
        ["NumPad8"] = 0x68,
        ["NumPad9"] = 0x69
    };

    public static bool TryParse(string? text, out HotkeyCombo? combo)
    {
        combo = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        string? keyToken = null;

        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];

            if (i < tokens.Length - 1)
            {
                if (!ModifierTokens.TryGetValue(token, out var mod))
                {
                    return false;
                }

                modifiers |= mod;
            }
            else
            {
                keyToken = token;
            }
        }

        if (keyToken is null || !TryResolveKey(keyToken, out var virtualKey))
        {
            return false;
        }

        // A lone Shift/Win modifier would hijack normal typing; require Ctrl or Alt.
        if ((modifiers & HotkeyModifiers.Ctrl) == 0 && (modifiers & HotkeyModifiers.Alt) == 0)
        {
            return false;
        }

        combo = new HotkeyCombo(modifiers, virtualKey, Canonicalize(modifiers, keyToken));
        return true;
    }

    public static HotkeyCombo? TryParse(string? text) => TryParse(text, out var combo) ? combo : null;

    public static bool TryValidate(string? text, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "الاختصار فارغ.";
            return false;
        }

        var tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            error = "يجب أن يحتوي الاختصار على مفتاح تعديل (Ctrl أو Alt مثلاً) ومفتاح آخر.";
            return false;
        }

        var keyToken = tokens[^1];
        if (keyToken.Equals("F12", StringComparison.OrdinalIgnoreCase))
        {
            error = ReservedKeyError;
            return false;
        }

        if (!ModifierTokens.ContainsKey(tokens[0]))
        {
            error = $"مفتاح التعديل \"{tokens[0]}\" غير معروف. استخدم Ctrl أو Alt أو Shift أو Win.";
            return false;
        }

        if (!TryParse(text, out _))
        {
            error = "صيغة الاختصار غير صالحة أو المفتاح غير مدعوم.";
            return false;
        }

        return true;
    }

    public static string Canonicalize(HotkeyModifiers modifiers, string keyToken)
    {
        var parts = new List<string>(4);

        if ((modifiers & HotkeyModifiers.Ctrl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & HotkeyModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & HotkeyModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & HotkeyModifiers.Win) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(NormalizeKeyDisplay(keyToken));
        return string.Join("+", parts);
    }

    private static string NormalizeKeyDisplay(string keyToken)
    {
        if (keyToken.Length == 1 && char.IsLetterOrDigit(keyToken[0]))
        {
            return char.ToUpperInvariant(keyToken[0]).ToString();
        }

        foreach (var known in NamedKeys.Keys)
        {
            if (known.Equals(keyToken, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        if (keyToken.Length >= 2 &&
            keyToken[0] is 'F' or 'f' &&
            int.TryParse(keyToken.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var fn) &&
            fn is >= 1 and <= 11)
        {
            return "F" + fn.ToString(CultureInfo.InvariantCulture);
        }

        return keyToken;
    }

    private static bool TryResolveKey(string token, out int virtualKey)
    {
        virtualKey = 0;

        if (token.Equals("F12", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                virtualKey = c; // VK codes for A-Z match their ASCII values
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                virtualKey = c; // VK codes for 0-9 match their ASCII values
                return true;
            }

            return false;
        }

        if (NamedKeys.TryGetValue(token, out var named))
        {
            virtualKey = named;
            return true;
        }

        if (token[0] is 'F' or 'f' &&
            int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var fn) &&
            fn is >= 1 and <= 11)
        {
            virtualKey = 0x70 + (fn - 1); // VK_F1 .. VK_F11
            return true;
        }

        return false;
    }
}
