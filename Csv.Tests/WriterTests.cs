using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class WriterTests
    {
        [TestMethod]
        public void EmptyCsv()
        {
            CheckOutput(new string[0], Enumerable.Empty<string[]>(), "\r\n");
        }

        [TestMethod]
        public void HeaderOnly()
        {
            CheckOutput(new[] { "A", "B", "C" }, Enumerable.Empty<string[]>(), "A,B,C\r\n");
        }

        [TestMethod]
        public void HeaderAndRows()
        {
            CheckOutput(new[] { "A", "B", "C" }, Enumerable.Repeat(new[] { "X", "Y", "Z" }, 2), "A,B,C\r\nX,Y,Z\r\nX,Y,Z\r\n");
        }

        [TestMethod]
        public void HeaderAndRowsWithNotEnoughColumns()
        {
            CheckOutput(new[] { "A", "B", "C" }, Enumerable.Repeat(new[] { "X" }, 2), "A,B,C\r\nX,,\r\nX,,\r\n");
        }

        [TestMethod]
        public void HeaderAndRowsEscapedValues()
        {
            CheckOutput(new[] { "A,", "\"B", "C\"", "D'" }, Enumerable.Repeat(new[] { "X", "Y", "Z" }, 2), "\"A,\",\"\"\"B\",\"C\"\"\",\"D'\"\r\nX,Y,Z,\r\nX,Y,Z,\r\n");
        }

        private static void CheckOutput(string[] headers, IEnumerable<string[]> lines, string expectedCsv)
        {
            var writer = new StringWriter();
            CsvWriter.Write(writer, headers, lines);
            var result = writer.ToString();

            Assert.AreEqual(expectedCsv, result);
            Assert.AreEqual(expectedCsv, CsvWriter.WriteToText(headers, lines));

            // NOTE: Parse again and check headers
            var reader = CsvReader.ReadFromText(result).ToArray();
            if (reader.Length > 0 && !reader[0].Headers.SequenceEqual(headers))
            {
                Assert.Fail("reader[0].Headers.SequenceEqual(headers)");
            }
        }
    }
}