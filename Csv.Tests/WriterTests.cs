using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Csv.Tests
{
    [TestClass]
    public class WriterTests
    {
        [TestMethod]
        public void EmptyCsv()
        {
            CheckOutput([], [], Environment.NewLine);
        }

        [TestMethod]
        public void HeaderOnly()
        {
            CheckOutput(["A", "B", "C"], [], $"A,B,C{Environment.NewLine}");
        }

        [TestMethod]
        public void HeaderAndRows()
        {
            CheckOutput(["A", "B", "C"], Enumerable.Repeat(new[] { "X", "Y", "Z" }, 2),
                $"A,B,C{Environment.NewLine}X,Y,Z{Environment.NewLine}X,Y,Z{Environment.NewLine}");
        }

        [TestMethod]
        public void HeaderAndRowsWithNotEnoughColumns()
        {
            CheckOutput(["A", "B", "C"], Enumerable.Repeat(new[] { "X" }, 2),
                $"A,B,C{Environment.NewLine}X,,{Environment.NewLine}X,,{Environment.NewLine}");
        }

        [TestMethod]
        public void HeaderAndRowsEscapedValues()
        {
            CheckOutput(["A,", "\"B", "C\"", "D'"], Enumerable.Repeat(new[] { "X", "Y", "Z" }, 2),
                $"\"A,\",\"\"\"B\",\"C\"\"\",\"D'\"{Environment.NewLine}X,Y,Z,{Environment.NewLine}X,Y,Z,{Environment.NewLine}");
        }

        //[TestMethod]
        //public void RowsNewLineEscapedValues()
        //{
        //    CheckOutput(new[] { "A", "B", "C" }, Enumerable.Repeat(new[] { "X\nY", "Y\r\n", "Z" }, 2), "A,B,C\r\n\"X\nY\",\"Y\r\n\",Z\r\n\"X\nY\",\"Y\r\n\",Z\r\n");
        //}

        [TestMethod]
        public void DontEscapeCommaForCustomSeparator()
        {
            CheckOutput(["A", "B", "C"], Enumerable.Repeat(new[] { "X,", "Y;", "Z" }, 2),
                $"A;B;C{Environment.NewLine}X,;\"Y;\";Z{Environment.NewLine}X,;\"Y;\";Z{Environment.NewLine}", ';');
        }

        private static void CheckOutput(string[] headers, IEnumerable<string[]> lines, string expectedCsv, char separator = ',')
        {
            var rows = lines.ToArray();

            var writer = new StringWriter();
            CsvWriter.Write(writer, headers, rows, separator);
            var result = writer.ToString();

            Assert.AreEqual(expectedCsv, result);
            Assert.AreEqual(expectedCsv, CsvWriter.WriteToText(headers, rows, separator));

            // NOTE: Parse again and check headers
            var reader = CsvReader.ReadFromText(result).ToArray();
            Assert.HasCount(rows.Length, reader);
            if (reader.Length > 0 && !reader[0].Headers.SequenceEqual(headers))
            {
                Assert.Fail("reader[0].Headers.SequenceEqual(headers)");
            }
        }
    }
}