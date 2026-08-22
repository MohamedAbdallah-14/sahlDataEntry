using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace SahelBundleKeyboard.Infrastructure.ImportExport;

/// <summary>
/// Writes a minimal valid .xlsx workbook containing a single worksheet with the
/// Arabic template header row (inline strings, RTL sheet view). No third-party library.
/// </summary>
public static class XlsxTemplateWriter
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRelations = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static void Write(string destinationPath)
    {
        using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
        WriteEntry(archive, "_rels/.rels", BuildRootRels());
        WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRels());
        WriteEntry(archive, "xl/styles.xml", BuildStyles());
        WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheet(CsvTemplateWriter.TemplateHeaders));
    }

    public static byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
            WriteEntry(archive, "_rels/.rels", BuildRootRels());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRels());
            WriteEntry(archive, "xl/styles.xml", BuildStyles());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSheet(CsvTemplateWriter.TemplateHeaders));
        }

        return ms.ToArray();
    }

    internal static string BuildSheet(IReadOnlyList<string> headers)
    {
        var sheetData = new XElement(Main + "sheetData",
            new XElement(Main + "row",
                new XAttribute("r", 1),
                headers.Select((header, i) =>
                    new XElement(Main + "c",
                        new XAttribute("r", ColumnIndexToLetter(i) + "1"),
                        new XAttribute("t", "inlineStr"),
                        new XElement(Main + "is",
                            new XElement(Main + "t", header))))));

        var sheet = new XElement(Main + "worksheet",
            new XElement(Main + "sheetViews",
                new XElement(Main + "sheetView",
                    new XAttribute("rightToLeft", true),
                    new XAttribute("workbookViewId", 0))),
            new XElement(Main + "sheetFormatPr",
                new XAttribute("defaultRowHeight", 15)),
            sheetData);

        return sheet.ToStringWithDeclaration();
    }

    private static string BuildContentTypes() =>
        new XElement(ContentTypes + "Types",
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "rels"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "xml"),
                new XAttribute("ContentType", "application/xml")),
            new XElement(ContentTypes + "Override",
                new XAttribute("PartName", "/xl/workbook.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
            new XElement(ContentTypes + "Override",
                new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
            new XElement(ContentTypes + "Override",
                new XAttribute("PartName", "/xl/styles.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))).ToStringWithDeclaration();

    private static string BuildRootRels() =>
        new XElement(PkgRelations + "Relationships",
            new XElement(PkgRelations + "Relationship",
                new XAttribute("Id", "rId1"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                new XAttribute("Target", "xl/workbook.xml"))).ToStringWithDeclaration();

    private static string BuildWorkbook() =>
        new XElement(Main + "workbook",
            new XAttribute(XNamespace.Xmlns + "r", R),
            new XElement(Main + "sheets",
                new XElement(Main + "sheet",
                    new XAttribute("name", "الحزم"),
                    new XAttribute("sheetId", 1),
                    new XAttribute(R + "id", "rId1")))).ToStringWithDeclaration();

    private static string BuildWorkbookRels() =>
        new XElement(PkgRelations + "Relationships",
            new XElement(PkgRelations + "Relationship",
                new XAttribute("Id", "rId1"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                new XAttribute("Target", "worksheets/sheet1.xml")),
            new XElement(PkgRelations + "Relationship",
                new XAttribute("Id", "rId2"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                new XAttribute("Target", "styles.xml"))).ToStringWithDeclaration();

    private static string BuildStyles() =>
        new XElement(Main + "styleSheet",
            new XElement(Main + "fonts", new XAttribute("count", 1), new XElement(Main + "font")),
            new XElement(Main + "fills", new XAttribute("count", 1), new XElement(Main + "fill")),
            new XElement(Main + "borders", new XAttribute("count", 1), new XElement(Main + "border")),
            new XElement(Main + "cellStyleXfs", new XAttribute("count", 1), new XElement(Main + "xf")),
            new XElement(Main + "cellXfs",
                new XAttribute("count", 1),
                new XElement(Main + "xf", new XAttribute("xfId", 0)))).ToStringWithDeclaration();

    public static string ColumnIndexToLetter(int index)
    {
        var letters = string.Empty;
        index++;
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            letters = (char)('A' + rem) + letters;
            index = (index - rem - 1) / 26;
        }

        return letters;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
