using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class IssuesTests
    {
        [TestMethod]
        [TestCategory("Issues")]
        public void Issue_73_EndingCommaInQuotedTextFails()
        {
            var options = new CsvOptions
            {
                Separator = ',',
                HeaderMode = HeaderMode.HeaderAbsent,
                AllowNewLineInEnclosedFieldValues = true,
            };
            string input = """Normal,"quoted with nested ""double"" quotes, and comma at the end,",normal 3,normal 4,normal 5""";
            var data = CsvReader.ReadFromText(input, options).First();
            Assert.AreEqual(5, data.ColumnCount);
            Assert.AreEqual("Normal", data[0]);
            Assert.AreEqual("quoted with nested \"double\" quotes, and comma at the end,", data[1]);
            Assert.AreEqual("normal 3", data[2]);
            Assert.AreEqual("normal 4", data[3]);
            Assert.AreEqual("normal 5", data[4]);
        }

        [TestMethod]
        [TestCategory("Issues")]
        public void Issue_72_HeaderAbsent_WithMultilineField_ShouldHaveCorrectHeaderCount()
        {
            // Arrange
            string importDataString = "Test;\"A\nB\nC\nD\nE\nF\nG\nH\";testing with very long string;123123";
            var options = new CsvOptions
            {
                Separator = ';',
                HeaderMode = HeaderMode.HeaderAbsent,
                AllowNewLineInEnclosedFieldValues = true,
                AllowBackSlashToEscapeQuote = false,
            };

            // Act
            var records = CsvReader.ReadFromText(importDataString, options).ToArray();

            // Assert
            Assert.HasCount(1, records, "Should have 1 record");
            var record = records[0];

            Assert.AreEqual(4, record.ColumnCount, "Should have 4 columns");
            Assert.HasCount(4, record.Headers, "Headers should have 4 elements");
            Assert.HasCount(4, record.Values, "Values should have 4 elements");

            // Verify the values are parsed correctly
            Assert.AreEqual("Test", record.Values[0]);
            Assert.AreEqual("A\r\nB\r\nC\r\nD\r\nE\r\nF\r\nG\r\nH", record.Values[1]);
            Assert.AreEqual("testing with very long string", record.Values[2]);
            Assert.AreEqual("123123", record.Values[3]);

            // Verify headers are generated correctly
            Assert.AreEqual("Column1", record.Headers[0]);
            Assert.AreEqual("Column2", record.Headers[1]);
            Assert.AreEqual("Column3", record.Headers[2]);
            Assert.AreEqual("Column4", record.Headers[3]);
        }

        [TestMethod]
        [TestCategory("Issues")]
        public void Issue_95_EmptyHeadersAtTail_AutoRenamedCorrectly()
        {
            // Arrange - CSV with trailing empty headers (from issue #95)
            var csv = @"#;Type;Subtype;Channel;Result_code;Created_by;Created_at;;;;;
H;Other;Other subtype;Example;0;username;2025-06-04;;;;;
#;Data1;Data2;Data3;Nota;AsesorDestino
D;1;2;3;;
";

            var options = new CsvOptions
            {
                Separator = ';',
#if NET8_0_OR_GREATER
                SkipRow = (row, idx) => row.Length > 0 && row.Span[0] == '#',
#else
                SkipRow = (row, idx) => row.Length > 0 && row[0] == '#',
#endif
                TrimData = true,
                // AutoRenameHeaders is true by default
            };

            // Act - This should now succeed with auto-renamed empty headers
            var lines = CsvReader.ReadFromText(csv, options).ToList();

            // Assert
            Assert.HasCount(1, lines);
            var line = lines[0];

            // Verify headers were renamed - the header row starts with "H" in this CSV
            var headers = line.Headers;
            Assert.AreEqual("H", headers[0]);
            Assert.AreEqual("Other", headers[1]);
            Assert.AreEqual("Other subtype", headers[2]);
            Assert.AreEqual("Example", headers[3]);
            Assert.AreEqual("0", headers[4]);
            Assert.AreEqual("username", headers[5]);
            Assert.AreEqual("2025-06-04", headers[6]);
            Assert.AreEqual("Empty", headers[7]);
            Assert.AreEqual("Empty2", headers[8]);
            Assert.AreEqual("Empty3", headers[9]);
            Assert.AreEqual("Empty4", headers[10]);
            Assert.AreEqual("Empty5", headers[11]);

            // Verify data access works - the data row starts with "D"
            Assert.AreEqual("D", line["H"]);
            Assert.AreEqual("1", line["Other"]);
            Assert.AreEqual("2", line["Other subtype"]);
        }

        [TestMethod]
        [TestCategory("Issues")]
        public void Issue_95_EmptyHeadersAtTail_ThrowsWhenAutoRenameDisabled()
        {
            // Arrange - Same CSV as above but with AutoRenameHeaders = false
            var csv = @"#;Type;Subtype;Channel;Result_code;Created_by;Created_at;;;;;
H;Other;Other subtype;Example;0;username;2025-06-04;;;;;
#;Data1;Data2;Data3;Nota;AsesorDestino
D;1;2;3;;
";

            var options = new CsvOptions
            {
                Separator = ';',
#if NET8_0_OR_GREATER
                SkipRow = (row, idx) => row.Length > 0 && row.Span[0] == '#',
#else
                SkipRow = (row, idx) => row.Length > 0 && row[0] == '#',
#endif
                TrimData = true,
                AutoRenameHeaders = false, // Disable auto-rename to get old behavior
            };

            // Act & Assert - Should throw with AutoRenameHeaders disabled
            var ex = Assert.ThrowsExactly<System.InvalidOperationException>(() =>
            {
                var lines = CsvReader.ReadFromText(csv, options).ToList();
            });

            Assert.IsTrue(ex.Message.Contains("Duplicate headers detected"),
                "Expected duplicate headers error message");
        }
    }
}
