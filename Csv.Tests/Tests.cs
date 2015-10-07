using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void EmptyCsv()
        {
            Assert.AreEqual(0, CsvReader.ReadFromText("A").Count());
        }

        [TestMethod]
        public void EmptyFile()
        {
            Assert.IsNull(CsvReader.ReadFromText(string.Empty).FirstOrDefault());
        }

        [TestMethod]
        public void DetectComma()
        {
            var lines = CsvReader.ReadFromText("A,B\nC,D").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(2, lines[0].Headers.Length);
            Assert.AreEqual("C", lines[0][0]);
            Assert.AreEqual("C", lines[0]["A"]);
            Assert.AreEqual("D", lines[0][1]);
            Assert.AreEqual("D", lines[0]["B"]);
        }

        [TestMethod]
        public void DetectSemicolon()
        {
            var lines = CsvReader.ReadFromText("A;B\nC;D").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(2, lines[0].Headers.Length);
            Assert.AreEqual("C", lines[0][0]);
            Assert.AreEqual("C", lines[0]["A"]);
            Assert.AreEqual("D", lines[0][1]);
            Assert.AreEqual("D", lines[0]["B"]);
        }

        [TestMethod]
        public void DetectTab()
        {
            var lines = CsvReader.ReadFromText("A\tB\nC\tD").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(2, lines[0].Headers.Length);
            Assert.AreEqual("C", lines[0][0]);
            Assert.AreEqual("C", lines[0]["A"]);
            Assert.AreEqual("D", lines[0][1]);
            Assert.AreEqual("D", lines[0]["B"]);
        }

        [TestMethod]
        public void UnescapeHeaders()
        {
            var lines = CsvReader.ReadFromText("'A';\"B'\"\nC;D").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(2, lines[0].Headers.Length);
            Assert.AreEqual("C", lines[0][0]);
            Assert.AreEqual("C", lines[0]["A"]);
            Assert.AreEqual("D", lines[0][1]);
            Assert.AreEqual("D", lines[0]["B'"]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void CustomChar()
        {
            var lines = CsvReader.ReadFromText("A|B\nC|D", new CsvOptions { Separator = '|' }).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(2, lines[0].Headers.Length);
            Assert.AreEqual("C", lines[0][0]);
            Assert.AreEqual("C", lines[0]["A"]);
            Assert.AreEqual("D", lines[0][1]);
            Assert.AreEqual("D", lines[0]["B"]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void TrimData()
        {
            var lines = CsvReader.ReadFromText(" A , B ,  C\n1   ,2   ,3\n   4,5,    6", new CsvOptions { TrimData = true }).ToArray();
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("1", lines[0][0]);
            Assert.AreEqual("1", lines[0]["A"]);
            Assert.AreEqual("4", lines[1]["A"]);
            Assert.AreEqual("6", lines[1][2]);
            Assert.AreEqual("6", lines[1]["C"]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void DontTrimData()
        {
            var lines = CsvReader.ReadFromText(" A , B ,  C\n1   ,2   ,3", new CsvOptions { TrimData = false }).ToArray();
            Assert.AreEqual("1   ", lines[0][0]);
            Assert.AreEqual("1   ", lines[0][" A "]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void TrimDataRawLine()
        {
            var lines = CsvReader.ReadFromText(" A , B ,  C\n1   ,2   ,3", new CsvOptions { TrimData = true }).ToArray();
            Assert.AreEqual("1   ,2   ,3", lines[0].Raw);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void RowsToSkip()
        {
            var lines = CsvReader.ReadFromText("skip this\nand this\nA,B,C\n1,2,3\n4,5,6", new CsvOptions { RowsToSkip = 2 }).ToArray();
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("1", lines[0][0]);
            Assert.AreEqual("1", lines[0]["A"]);
            Assert.AreEqual("4", lines[1]["A"]);
            Assert.AreEqual("6", lines[1][2]);
            Assert.AreEqual("6", lines[1]["C"]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void SkipRow()
        {
            var lines = CsvReader.ReadFromText("//comment\nA,B,C\n1,2,3\n4,5,6", new CsvOptions { SkipRow = (row, idx) => row.StartsWith("//") }).ToArray();
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("1", lines[0][0]);
            Assert.AreEqual("1", lines[0]["A"]);
            Assert.AreEqual("4", lines[1]["A"]);
            Assert.AreEqual("6", lines[1][2]);
            Assert.AreEqual("6", lines[1]["C"]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void SkipEmptyRows()
        {
            var lines = CsvReader.ReadFromText("\n#comment\nA,B,C\n1,2,3\n4,5,6\n\n").ToArray();
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("1", lines[0][0]);
            Assert.AreEqual("1", lines[0]["A"]);
            Assert.AreEqual("4", lines[1]["A"]);
            Assert.AreEqual("6", lines[1][2]);
            Assert.AreEqual("6", lines[1]["C"]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void InvalidHeader()
        {
            var lines = CsvReader.ReadFromText("A\n1").ToArray();
            var b = lines[0]["B"];
            Assert.Fail();
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void IgnoreHeaderCasing()
        {
            var lines = CsvReader.ReadFromText("A,B,C\n1,2,3\n4,5,6", new CsvOptions { Comparer = StringComparer.OrdinalIgnoreCase }).ToArray();
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("1", lines[0][0]);
            Assert.AreEqual("1", lines[0]["a"]);
            Assert.AreEqual("4", lines[1]["a"]);
            Assert.AreEqual("6", lines[1][2]);
            Assert.AreEqual("6", lines[1]["c"]);
        }

        [TestMethod]
        public void QuotedValueSameSeparator()
        {
            var lines = CsvReader.ReadFromText("A,B\n\"1,2,3\",4").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("1,2,3", lines[0]["A"]);
            Assert.AreEqual("4", lines[0]["B"]);
        }

        [TestMethod]
        public void QuotedValueDifferentSeparator()
        {
            var lines = CsvReader.ReadFromText("A;B\n\"1,2,3\";4").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("1,2,3", lines[0]["A"]);
        }
    }
}