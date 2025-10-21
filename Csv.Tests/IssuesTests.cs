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
    }
}
