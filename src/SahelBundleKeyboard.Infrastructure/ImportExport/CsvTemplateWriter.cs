using System.IO;
using System.Text;

namespace SahelBundleKeyboard.Infrastructure.ImportExport;

/// <summary>Writes the blank import template as CSV (UTF-8 with BOM so Excel reads Arabic correctly).</summary>
public static class CsvTemplateWriter
{
    public static readonly string[] TemplateHeaders =
    [
        "كود المنتج (اختياري)",
        "اسم المنتج (إجباري)",
        "الكمية (إجباري)",
        "السعر المخصص (اختياري)"
    ];

    public static void Write(string destinationPath)
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF'); // BOM: Excel detects UTF-8 and renders Arabic correctly
        sb.AppendLine(string.Join(",", TemplateHeaders.Select(Escape)));
        File.WriteAllText(destinationPath, sb.ToString(), new UTF8Encoding(false));
    }

    public static string Escape(string field)
    {
        if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
