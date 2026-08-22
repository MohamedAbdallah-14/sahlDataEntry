using SahelBundleKeyboard.Infrastructure.ImportExport;

namespace SahelBundleKeyboard.Infrastructure.Tests.ImportExport;

public class CsvRoundTripTests
{
    [Fact]
    public void Template_CanBeWrittenAndReReadWithRecognizedHeaders()
    {
        var path = Path.Combine(Path.GetTempPath(), "sbk-template-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            CsvTemplateWriter.Write(path);

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "Template CSV must start with a UTF-8 BOM so Excel renders Arabic correctly.");

            var content = File.ReadAllText(path); // BOM is consumed by the text reader
            var result = RowMapper.FromGridRows(CsvParser.Parse(content));
            Assert.False(result.FileLevelError, result.FileError);
            Assert.Empty(result.Rows); // blank template: headers only
        }
        finally
        {
            File.Delete(path);
        }
    }
}
