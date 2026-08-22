using System.Globalization;
using System.Text;

namespace SahelBundleKeyboard.Core.Sequencing;

/// <summary>
/// Formats decimal quantities/prices as clean invariant strings with a dot separator:
/// no scientific notation, no thousands separators, no unnecessary trailing zeros.
/// "2.000" -> "2", "1.2500" -> "1.25", "1.5" stays "1.5".
/// </summary>
public static class QuantityFormatter
{
    // decimal supports at most 28 fractional digits; '#' trims trailing zeros.
    private static readonly string DecimalFormat = "0." + new string('#', 28);

    public static string Format(decimal value)
    {
        return value.ToString(DecimalFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses user input into a decimal. Accepts Latin and Arabic-Indic digits and the
    /// Arabic decimal separators (U+066B, U+066C family); output formatting is always invariant.
    /// Returns false for anything that is not a plain number.
    /// </summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = NormalizeDigits(text.Trim());
        if (normalized.Length == 0)
        {
            return false;
        }

        // Replace Arabic decimal separator with dot before strict parsing.
        normalized = normalized.Replace('\u066B', '.').Replace('\u066C', '.');

        if (normalized.Any(c => c == ','))
        {
            // Comma is ambiguous (thousands vs decimal); reject it to never guess values.
            return false;
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    public static string NormalizeDigits(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                >= '٠' and <= '٩' => (char)(c - '٠' + '0'),   // U+0660..U+0669
                >= '۰' and <= '۹' => (char)(c - '۰' + '0'),   // Extended Arabic-Indic
                _ => c
            });
        }

        return sb.ToString();
    }
}
