using SahelBundleKeyboard.Infrastructure.ImportExport;

namespace SahelBundleKeyboard.Infrastructure.Tests.ImportExport;

public class XlsxTests
{
    [Fact]
    public void TemplateWriter_Output_RoundTripsThroughReader()
    {
        var path = Path.Combine(Path.GetTempPath(), "sbk-template-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            File.WriteAllBytes(path, XlsxTemplateWriter.ToBytes());

            var rows = XlsxReader.ReadRows(path);

            Assert.Single(rows);
            Assert.Equal(CsvTemplateWriter.TemplateHeaders, rows[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Reader_ParsesSharedStringsNumbersAndInlineStrings()
    {
        var path = Path.Combine(Path.GetTempPath(), "sbk-sheet-" + Guid.NewGuid().ToString("N") + ".xlsx");

        // Hand-build a workbook with sharedStrings + numeric cells to exercise the reader.
        using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
        {
            WriteEntry(zip, "[Content_Types].xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            WriteEntry(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            WriteEntry(zip, "xl/workbook.xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="S" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            WriteEntry(zip, "xl/_rels/workbook.xml.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            WriteEntry(zip, "xl/sharedStrings.xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="3" uniqueCount="3"><si><t>كود المنتج (اختياري)</t></si><si><t>اسم المنتج (إجباري)</t></si><si><t>زيت عافية ١ لتر</t></si></sst>""");
            WriteEntry(zip, "xl/worksheets/sheet1.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="s"><v>0</v></c>
                      <c r="B1" t="s"><v>1</v></c>
                      <c r="C1" t="inlineStr"><is><t>الكمية (إجباري)</t></is></c>
                      <c r="D1" t="inlineStr"><is><t>السعر المخصص (اختياري)</t></is></c>
                    </row>
                    <row r="2">
                      <c r="B2" t="s"><v>2</v></c>
                      <c r="C2"><v>1.5</v></c>
                      <c r="D2"><v>42.25</v></c>
                    </row>
                  </sheetData>
                </worksheet>
                """);
        }

        try
        {
            var result = RowMapper.FromGridRows(XlsxReader.ReadRows(path));

            Assert.False(result.FileLevelError, result.FileError);
            Assert.Equal(1, result.ValidCount);

            var item = RowMapper.ToItems(result.Rows.Where(r => r.IsValid).ToList())[0];
            Assert.Equal("زيت عافية ١ لتر", item.ProductName); // shared string resolved from column B
            Assert.Equal(string.Empty, item.ProductCode);
            Assert.Equal(1.5m, item.BaseQuantity);
            Assert.Equal(42.25m, item.CustomPrice);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ColumnIndexToLetter_MatchesSpreadsheetRefs()
    {
        Assert.Equal("A", XlsxTemplateWriter.ColumnIndexToLetter(0));
        Assert.Equal("Z", XlsxTemplateWriter.ColumnIndexToLetter(25));
        Assert.Equal("AA", XlsxTemplateWriter.ColumnIndexToLetter(26));
        Assert.Equal("BC", XlsxTemplateWriter.ColumnIndexToLetter(54));
    }

    private static void WriteEntry(System.IO.Compression.ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }
}
