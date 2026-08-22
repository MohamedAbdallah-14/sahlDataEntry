using System.IO;
using System.IO.Compression;
using System.Text;

namespace SahelBundleKeyboard.Infrastructure.ImportExport;

/// <summary>
/// Minimal hand-rolled XLSX reader (no third-party dependency). Supports shared strings,
/// inline strings and plain numeric cells from the first worksheet. Rows are returned as
/// dense string lists aligned to their spreadsheet column positions.
/// </summary>
public static class XlsxReader
{
    public static IReadOnlyList<IReadOnlyList<string>> ReadRows(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);

        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = FindFirstWorksheet(archive);

        var rows = new List<IReadOnlyList<string>>();

        using (var stream = sheetEntry.Open())
        using (var xml = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { IgnoreWhitespace = true }))
        {
            var currentCells = new List<(int Col, string Value)>();
            var valueText = new StringBuilder();
            var rowIndex = 0;

            while (xml.Read())
            {
                if (xml.NodeType != System.Xml.XmlNodeType.Element || xml.LocalName != "row")
                {
                    continue;
                }

                var declaredRowNumber = ParsePositiveInt(xml.GetAttribute("r"));
                rowIndex = declaredRowNumber ?? rowIndex + 1;

                currentCells.Clear();

                if (!xml.IsEmptyElement)
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == System.Xml.XmlNodeType.EndElement && xml.LocalName == "row")
                        {
                            break;
                        }

                        if (xml.NodeType == System.Xml.XmlNodeType.Element && xml.LocalName == "c")
                        {
                            var colIndex = ColumnLetterToIndex(CellColumnLetter(xml.GetAttribute("r")));
                            var typeAttr = xml.GetAttribute("t") ?? "n";
                            var rawValue = ReadCellValue(xml, typeAttr, sharedStrings, valueText);

                            if (rawValue.Length > 0)
                            {
                                currentCells.Add((colIndex, rawValue));
                            }
                        }
                    }
                }

                var rowValues = MaterializeRow(currentCells);
                if (rowValues.Any(v => v.Length > 0))
                {
                    while (rows.Count < rowIndex)
                    {
                        rows.Add([]);
                    }

                    rows[rowIndex - 1] = rowValues;
                }
            }
        }

        // Drop trailing empty rows so the mapper only sees the used range.
        while (rows.Count > 0 && rows[^1].All(v => v.Trim().Length == 0))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }

    private static string ReadCellValue(System.Xml.XmlReader cellXml, string typeAttr, List<string> sharedStrings, StringBuilder valueText)
    {
        valueText.Clear();

        if (cellXml.IsEmptyElement)
        {
            return string.Empty;
        }

        // ReadSubtree confines scanning to this <c> element; without it
        // ReadElementContentAsString skips past </c> and merges later cells.
        using var sub = cellXml.ReadSubtree();
        while (sub.Read())
        {
            if (sub.NodeType == System.Xml.XmlNodeType.Element &&
                sub.LocalName is "v" or "t")
            {
                valueText.Append(sub.ReadElementContentAsString());
            }
        }

        var raw = valueText.ToString();

        return typeAttr switch
        {
            "s" => int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty,
            "b" => raw == "1" ? "TRUE" : "FALSE",
            _ => raw
        };
    }

    private static List<string> MaterializeRow(List<(int Col, string Value)> cells)
    {
        if (cells.Count == 0)
        {
            return [];
        }

        var width = cells.Max(c => c.Col) + 1;
        var values = new string[width];
        Array.Fill(values, string.Empty);

        foreach (var (col, value) in cells)
        {
            values[col] = value;
        }

        return [.. values];
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        var strings = new List<string>();

        if (entry is null)
        {
            return strings;
        }

        using var stream = entry.Open();
        using var xml = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { IgnoreWhitespace = true });

        while (xml.Read())
        {
            if (xml.NodeType == System.Xml.XmlNodeType.Element && xml.LocalName == "si")
            {
                var sb = new StringBuilder();

                using var sub = xml.ReadSubtree();
                while (sub.Read())
                {
                    if (sub.NodeType == System.Xml.XmlNodeType.Element && sub.LocalName == "t")
                    {
                        sb.Append(sub.ReadElementContentAsString());
                    }
                }

                strings.Add(sb.ToString());
            }
        }

        return strings;
    }

    private static ZipArchiveEntry FindFirstWorksheet(ZipArchive archive)
    {
        var candidates = archive.Entries
            .Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                        Path.GetFileName(e.FullName).StartsWith("sheet", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => ParseLeadingNumber(Path.GetFileNameWithoutExtension(e.FullName)))
            .ToList();

        var first = candidates.FirstOrDefault()
            ?? throw new IOException("ملف Excel لا يحتوي أي ورقة عمل.");

        return first;
    }

    private static int ParseLeadingNumber(string text)
    {
        var digits = new string(text.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : int.MaxValue;
    }

    private static int? ParsePositiveInt(string? text) =>
        int.TryParse(text, out var n) && n > 0 ? n : null;

    private static string CellColumnLetter(string? cellRef)
    {
        if (string.IsNullOrEmpty(cellRef))
        {
            return "A";
        }

        var letters = new string(cellRef.TakeWhile(char.IsAsciiLetter).ToArray());
        return letters.Length == 0 ? "A" : letters.ToUpperInvariant();
    }

    internal static int ColumnLetterToIndex(string letters)
    {
        var index = 0;
        foreach (var c in letters)
        {
            index = (index * 26) + (c - 'A' + 1);
        }

        return index - 1;
    }
}
