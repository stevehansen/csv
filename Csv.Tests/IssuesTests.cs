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
        public void Issue_72_IncorrectHeaders()
        {
            string importDataString = "Test;\"A\nB\nC\nD\nE\nF\nG\nH\";testing with very long string;123123";
            var options = new CsvOptions
            {
                Separator = ';',
                HeaderMode = HeaderMode.HeaderAbsent,
                AllowNewLineInEnclosedFieldValues = true,
                AllowBackSlashToEscapeQuote = false,
            };

            var data = CsvReader.ReadFromText(importDataString, options).ToArray();
            Assert.AreEqual(1, data.Length);
            var headers = data[0].Headers;
            Assert.AreEqual(4, headers.Length);
        }
    }
}
