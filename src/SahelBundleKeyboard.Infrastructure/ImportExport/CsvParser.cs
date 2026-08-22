using System.Text;

namespace SahelBundleKeyboard.Infrastructure.ImportExport;

/// <summary>
/// RFC-4180-style CSV parser: quoted fields, escaped quotes, embedded commas/newlines,
/// CR/LF/CRLF line endings, trailing rows without newline.
/// </summary>
public static class CsvParser
{
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string content)
    {
        var rows = new List<IReadOnlyList<string>>();
        var currentRow = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldStarted = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    fieldStarted = true;
                    break;

                case ',':
                    currentRow.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    break;

                case '\r':
                case '\n':
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }

                    EndRow(rows, currentRow, field);
                    break;

                default:
                    field.Append(c);
                    fieldStarted = true;
                    break;
            }
        }

        if (fieldStarted || field.Length > 0 || currentRow.Count > 0)
        {
            EndRow(rows, currentRow, field);
        }

        return rows;
    }

    private static void EndRow(List<IReadOnlyList<string>> rows, List<string> currentRow, StringBuilder field)
    {
        currentRow.Add(field.ToString());
        field.Clear();
        rows.Add([.. currentRow]);
        currentRow.Clear();
    }
}
