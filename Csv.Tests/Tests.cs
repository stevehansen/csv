using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            Assert.AreEqual(2, lines[0].ColumnCount);
            Assert.AreEqual(2, lines[0].Index);
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
            var lines = CsvReader.ReadFromText("\"A\";\"B'\"\nC;D").ToArray();
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
            Assert.AreEqual(3, lines[1].Index);
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
        [TestCategory("CsvOptions")]
        public void ValidateColumnCount()
        {
            var lines = CsvReader.ReadFromText("A,B,C\n1,2").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("1", lines[0]["A"]);

            lines = [.. CsvReader.ReadFromText("A,B,C\n1,2", new CsvOptions { ValidateColumnCount = true })];
            Assert.AreEqual(1, lines.Length);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = lines[0]["A"]);
        }

        [TestMethod]
        public void InvalidHeader()
        {
            var lines = CsvReader.ReadFromText("A\n1").ToArray();
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = lines[0]["B"]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void FirstRowIsDataWhenHeaderAbsent()
        {
            var lines = CsvReader.ReadFromText("1,2,3\n4,5,6", new CsvOptions { HeaderMode = HeaderMode.HeaderAbsent }).ToArray();

            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("1", lines[0][0]);
            Assert.AreEqual("2", lines[0][1]);
            Assert.AreEqual("3", lines[0][2]);
            Assert.AreEqual("4", lines[1][0]);
            Assert.AreEqual("5", lines[1][1]);
            Assert.AreEqual("6", lines[1][2]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void AbsentHeaderIgnored()
        {
            var lines = CsvReader.ReadFromText("1,2,3\n4,5,6", new CsvOptions { HeaderMode = HeaderMode.HeaderAbsent }).ToArray();
            var headers = lines[0].Headers;

            Assert.AreNotEqual("1", headers[0]);
            Assert.AreNotEqual("2", headers[1]);
            Assert.AreNotEqual("3", headers[2]);
        }

        [TestMethod]
        [TestCategory("CsvOptions")]
        public void AbsentHeaderWarnDuplicate()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = CsvReader.ReadFromText(",,\n4,5,6").ToArray());
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

        [TestMethod]
        public void SingleQuotedValueSameSeparator()
        {
            var options = new CsvOptions
            {
                AllowSingleQuoteToEncloseFieldValues = true
            };
            var lines = CsvReader.ReadFromText("A,B\n'1,2,3',4", options).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("1,2,3", lines[0]["A"]);
            Assert.AreEqual("4", lines[0]["B"]);
        }

        [TestMethod]
        public void AllowEmptyValues()
        {
            var lines = CsvReader.ReadFromText("head1;head2\ntext1;").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("", lines[0]["head2"]);

            lines = [.. CsvReader.ReadFromText("head1;head2;head3\ntext1;;text3")];
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("", lines[0]["head2"]);
            Assert.AreEqual("text3", lines[0]["head3"]);

            lines = [.. CsvReader.ReadFromText("head1;head2;head3\ntext1;\"\";text3")];
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("", lines[0]["head2"]);
            Assert.AreEqual("text3", lines[0]["head3"]);
        }

        [TestMethod]
        public void AllowWhitespaceValues()
        {
            var lines = CsvReader.ReadFromText("head1;head2;head3;head4\n;  ;text3;").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("", lines[0]["head1"]);
            Assert.AreEqual("  ", lines[0]["head2"]);
            Assert.AreEqual("text3", lines[0]["head3"]);
            Assert.AreEqual("", lines[0]["head4"]);
        }

        [TestMethod]
        public void AllowSingleQuoteInsideValue()
        {
            var lines = CsvReader.ReadFromText("a;b;c\n\"'\";a'b;'").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("'", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void WithNewLineInQuotedFieldValue_MultipleRecordsHandledCorrectly()
        {
            const string input = @"ID,Notes,User
2,"" * Bullet 1
* Bullet 2
* Bullet 3
* Bullet 4
* Bullet 5
"",Joe
3,""* Bullet 1
* Bullet 2
* Bullet 3
* Bullet 4
* Bullet 5
"",Joe
";
            var options = new CsvOptions { AllowNewLineInEnclosedFieldValues = true };
            var records = CsvReader.ReadFromText(input, options).ToArray();
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(3, records[0].ColumnCount);
            Assert.AreEqual(3, records[1].ColumnCount);
        }

        [TestMethod]
        public void WithNewLineWithDelimiterInQuotedFieldValue_MultipleRecordsHandledCorrectly()
        {
            const string input = @"ID,Notes,User
2,"" * Bullet 1,
* Bullet 2,
* Bullet 3,
* Bullet 4,
* Bullet 5,
"",Joe
3,""* Bullet 1,
* Bullet 2,
* Bullet 3,
* Bullet 4,
* Bullet 5,
"",Joe
";
            var options = new CsvOptions { AllowNewLineInEnclosedFieldValues = true };
            var records = CsvReader.ReadFromText(input, options).ToArray();
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(3, records[0].ColumnCount);
            Assert.AreEqual(3, records[1].ColumnCount);
        }

        [TestMethod]
        public void WithNewLineWithEndingEmptyQuoted()
        {
            const string input = @"ID,Notes,User
2,"" * Bullet 1,
* Bullet 2,
* Bullet 3,
* Bullet 4,
* Bullet 5,
"",""""
3,""* Bullet 1,
* Bullet 2,
* Bullet 3,
* Bullet 4,
* Bullet 5,
"",Joe
";
            var options = new CsvOptions { AllowNewLineInEnclosedFieldValues = true };
            var records = CsvReader.ReadFromText(input, options).ToArray();
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(3, records[0].ColumnCount);
            Assert.AreEqual(string.Empty, records[0][2]);
            Assert.AreEqual(3, records[1].ColumnCount);
            Assert.AreEqual("Joe", records[1][2]);
        }

        [TestMethod]
        public void ThrowExceptionForUnknownHeaders()
        {
            var lines = CsvReader.ReadFromText("a;b\na;b").ToArray();
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = lines[0]["c"]);
        }

        [TestMethod]
        public void TestReturnEmptyForMissingColumn()
        {
            var lines = CsvReader.ReadFromText("a;b\na;b", new CsvOptions { ReturnEmptyForMissingColumn = true }).ToArray();
            var c = lines[0]["c"];
            Assert.AreEqual(string.Empty, c);
        }

        [TestMethod]
        public void TestHasHeader()
        {
            var lines = CsvReader.ReadFromText("a;b\na;b").ToArray();
            var line = lines[0];
            Assert.IsTrue(line.HasColumn("a"));
            Assert.IsFalse(line.HasColumn("c"));
        }

        [TestMethod]
        public void TestLineHasColumnMissing()
        {
            var lines = CsvReader.ReadFromText("a;b;c\n1;2").ToArray();
            var line = lines[0];
            Assert.IsTrue(line.HasColumn("c"));
            Assert.IsFalse(line.LineHasColumn("c"));
        }

        [TestMethod]
        public void TestLineHasColumnPresent()
        {
            var lines = CsvReader.ReadFromText("a;b;c\n1;2;3").ToArray();
            var line = lines[0];
            Assert.IsTrue(line.LineHasColumn("c"));
        }

        [TestMethod]
        public void ThrowExceptionForInvalidNumberOfCells()
        {
            var lines = CsvReader.ReadFromText("a;b;c\na;b").ToArray();
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = lines[0]["c"]);
        }

        [TestMethod]
        public void AllowDoubleQuoteInsideValue()
        {
            var lines = CsvReader.ReadFromText("a;b;c\n\"\"\"\";a'b;'").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("\"", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void WithAllowNewLineInEnclosedFieldValues_AllowLFInsideQuotedValue()
        {
            var options = new CsvOptions
            {
                AllowNewLineInEnclosedFieldValues = true,
                NewLine = "\r\n"
            };
            var lines = CsvReader.ReadFromText("a,b,c\n\"one\"\"\ntwo\",a'b,'", options).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("one\"\r\ntwo", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void WithAllowNewLineInEnclosedFieldValues_AllowCRLFInsideQuotedValue()
        {
            var options = new CsvOptions
            {
                AllowNewLineInEnclosedFieldValues = true,
                NewLine = "\n",
                AllowBackSlashToEscapeQuote = true
            };
            var lines = CsvReader.ReadFromText("a,b,c\n\"one\\\"\r\ntwo\",a'b,'", options).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("one\"\ntwo", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void WithAllowBackSlashToEscapeQuote()
        {
            var options = new CsvOptions
            {
                AllowBackSlashToEscapeQuote = true,
            };
            var lines = CsvReader.ReadFromText("a,b,c\n\"one\\\"two\",a'b,'", options).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("one\"two", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);

            options = new CsvOptions
            {
                AllowBackSlashToEscapeQuote = false,
            };
            lines = [.. CsvReader.ReadFromText("a,b,c\n\"one\\\"two\",a'b,'", options)];
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("one\\\"two", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void AllowSingleQuoteToEncloseFieldValues()
        {
            var options = new CsvOptions
            {
                AllowSingleQuoteToEncloseFieldValues = true,
                AllowNewLineInEnclosedFieldValues = true
            };
            var lines = CsvReader.ReadFromText("a,b,c,d\n'one\"\r\ntwo',a'b,'", options).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual($"one\"{Environment.NewLine}two", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void UnmatchedDoubleQuoteAtEndOfStringShouldNotCauseError()
        {
            var lines = CsvReader.ReadFromText("a,b,c\none two,a'b,\"").ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual($"one two", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("\"", lines[0]["c"]);
        }

        [TestMethod]
        public void TestReader()
        {
            var reader = new StringReader("a;b;c\n\"\"\"\";a'b;'");
            var lines = CsvReader.Read(reader).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("\"", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void TestStream()
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("a;b;c\n\"\"\"\";a'b;'"));
            var lines = CsvReader.ReadFromStream(stream).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("\"", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void TestAlias()
        {
            var lines = CsvReader.ReadFromText("a;b;c\n\"\"\"\";a'b;'", new CsvOptions { Aliases = [["d", "a"]] }).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("\"", lines[0]["a"]);
            Assert.AreEqual("\"", lines[0]["d"]);
            Assert.IsTrue(lines[0].HasColumn("a"));
            Assert.IsTrue(lines[0].HasColumn("d"));
            Assert.AreEqual(3, lines[0].ColumnCount);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void TestAliasAndValidateColumnCount()
        {
            var lines = CsvReader.ReadFromText("a;b;c\n\"\"\"\";a'b;'", new CsvOptions { Aliases = [["d", "a"]], ValidateColumnCount = true }).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("\"", lines[0]["a"]);
            Assert.AreEqual("\"", lines[0]["d"]);
            Assert.IsTrue(lines[0].HasColumn("a"));
            Assert.IsTrue(lines[0].HasColumn("d"));
            Assert.AreEqual(3, lines[0].ColumnCount);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
        }

        [TestMethod]
        public void TestAliasIgnoreMissingGroup()
        {
            var lines = CsvReader.ReadFromText("a;b;c\n\"\"\"\";a'b;'", new CsvOptions { Aliases = [["d", "e"]] }).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual("\"", lines[0]["a"]);
            Assert.AreEqual("a'b", lines[0]["b"]);
            Assert.AreEqual("'", lines[0]["c"]);
            Assert.IsFalse(lines[0].HasColumn("d"));
        }

        [TestMethod]
        public void TestAliasDuplicatesInGroup()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = CsvReader.ReadFromText("a;b;c\n\"\"\"\";a'b;'", new CsvOptions { Aliases = [["b", "a"]] }).ToArray());
        }

        [TestMethod]
        public void TestInternalSeparatorAfterEscapedQuote()
        {
            var options = new CsvOptions
            {
                HeaderMode = HeaderMode.HeaderAbsent,
            };
            foreach (var line in CsvReader.ReadFromText("one,\"two - a, two - b, \"\"two - c\"\", two - d\",three", options))
                Assert.AreEqual(3, line.Values.Length);
        }

        [TestMethod]
        public void ReadFromMemory()
        {
            var lines = CsvReader.ReadFromMemory("A,B\nC,D".AsMemory()).ToArray();
            Assert.AreEqual(1, lines.Length);
            Assert.AreEqual(2, lines[0].Headers.Length);
            Assert.AreEqual(2, lines[0].ColumnCount);
            Assert.AreEqual(2, lines[0].Index);
            Assert.AreEqual("C", lines[0][0].AsString());
            Assert.AreEqual("C", lines[0]["A"].AsString());
            Assert.AreEqual("D", lines[0][1].AsString());
            Assert.AreEqual("D", lines[0]["B"].AsString());
        }

        [TestMethod]
        public void BackslashBeforeQuote()
        {
            //"A";"B";"C"
            //"A """;"B \"" ";"C"
            var withSpace = CsvReader.ReadFromText("\"A\";\"B\";\"C\"\n\"A \"\"\";\"B \\\"\" \";\"C\"", new CsvOptions { AllowNewLineInEnclosedFieldValues = true }).ToArray();
            Assert.AreEqual(1, withSpace.Length);
            Assert.AreEqual(3, withSpace[0].Headers.Length);
            Assert.AreEqual("A \"", withSpace[0][0]);
            Assert.AreEqual("B \\\" ", withSpace[0][1]);
            Assert.AreEqual("C", withSpace[0][2]);

            withSpace = [.. CsvReader.ReadFromText("\"A\";\"B\";\"C\"\n\"A \"\"\";\"B \\\"\" \";\"C\"")];
            Assert.AreEqual(1, withSpace.Length);
            Assert.AreEqual(3, withSpace[0].Headers.Length);
            Assert.AreEqual("A \"", withSpace[0][0]);
            Assert.AreEqual("B \\\" ", withSpace[0][1]);
            Assert.AreEqual("C", withSpace[0][2]);

            //"A";"B";"C"
            //"A """;"B \""";"C"
            var withoutSpace = CsvReader.ReadFromText("\"A\";\"B\";\"C\"\n\"A \"\"\";\"B \\\"\"\";\"C\"", new CsvOptions { AllowNewLineInEnclosedFieldValues = true }).ToArray();
            Assert.AreEqual(1, withoutSpace.Length);
            Assert.AreEqual(3, withoutSpace[0].Headers.Length);
            Assert.AreEqual("A \"", withoutSpace[0][0]);
            Assert.AreEqual("B \\\"", withoutSpace[0][1]);
            Assert.AreEqual("C", withoutSpace[0][2]);

            withoutSpace = [.. CsvReader.ReadFromText("\"A\";\"B\";\"C\"\n\"A \"\"\";\"B \\\"\"\";\"C\"")];
            Assert.AreEqual(1, withoutSpace.Length);
            Assert.AreEqual(3, withoutSpace[0].Headers.Length);
            Assert.AreEqual("A \"", withoutSpace[0][0]);
            Assert.AreEqual("B \\\"", withoutSpace[0][1]);
            Assert.AreEqual("C", withoutSpace[0][2]);
        }
        public const string smalldatabase = @"A,B,C,D
1,34.47,Never,15
2,44.17,gonna,28
3,38.28,give,362
4,11.26,you,3992
5,96.73,up,65923
6,73.12,Never,64
7,89.64,gonna,95
8,40.32,let,1537
9,62.88,you,8463
10,3.94,down,442";
        [TestMethod]
        [TestCategory("GetColumn")]
        public void GetColumnTest_1()
        {
            var options = new CsvOptions();
            var database = CsvReader.ReadFromText(smalldatabase, options);
            var c1 = database.GetColumn(0);
            CollectionAssert.AreEqual(c1.ToArray(), Enumerable.Range(1, 10).Select(x => x.ToString()).ToArray());
        }
        [TestMethod]
        [TestCategory("GetColumn")]
        public void GetColumnTest_2()
        {
            var options = new CsvOptions();
            var database = CsvReader.ReadFromText(smalldatabase, options);
            var c2 = database.GetColumn(1).ToArray();
            Assert.AreEqual(10, c2.Length);
        }
        [TestMethod]
        [TestCategory("GetColumn")]
        public void GetColumnTest_3()
        {
            var options = new CsvOptions();
            var database = CsvReader.ReadFromText(smalldatabase, options);
            var c3 = database.GetColumn(2).ToArray();
            var merged = c3.Aggregate("", (x, y) => x + " " + y);
            Assert.AreEqual(" Never gonna give you up Never gonna let you down", merged);
        }
        [TestMethod]
        [TestCategory("GetColumn<T>")]
        public void GetColumnGenericTest_1()
        {
            var database = CsvReader.ReadFromText(smalldatabase);
            var c1 = database.GetColumn(0, x => int.Parse(x)).ToArray();
            CollectionAssert.AreEqual(c1, Enumerable.Range(1, 10).ToArray());
        }
        [TestMethod]
        [TestCategory("GetColumn<T>")]
        public void GetColumnGenericTest_2()
        {
            var database = CsvReader.ReadFromText(smalldatabase);
            var c2 = database.GetColumn(1, x => double.Parse(x)).ToArray();
            var sum = c2.Aggregate(0.0, (x, y) => x + y);
            Assert.AreEqual(494.81, sum);
        }
        [TestMethod]
        [TestCategory("GetColumn<T>")]
        public void GetColumnGenericTest_3()
        {
            var database = CsvReader.ReadFromText(smalldatabase);
            var c4 = database.GetColumn(3, x => int.Parse(x) % 10).ToArray();
            CollectionAssert.AreEqual(c4, new int[] { 5, 8, 2, 2, 3, 4, 5, 7, 3, 2 });
        }
        [TestMethod]
        [TestCategory("GetBlock")]
        public void GetBlockTest_1()
        {
            var database = CsvReader.ReadFromText(smalldatabase);
            var block = database.GetBlock(2, 2).ToArray();
            Assert.AreEqual(2, block.Length);
            Assert.AreEqual(4, block[0].Headers.Length);
            Assert.AreEqual("3", block[0]["A"]);
            Assert.AreEqual("38.28", block[0]["B"]);
            Assert.AreEqual("give", block[0]["C"]);
            Assert.AreEqual("362", block[0]["D"]);
            Assert.AreEqual("4", block[1]["A"]);
            Assert.AreEqual("11.26", block[1]["B"]);
            Assert.AreEqual("you", block[1]["C"]);
            Assert.AreEqual("3992", block[1]["D"]);
        }
        [TestMethod]
        [TestCategory("GetBlock")]
        public void GetBlockTest_2()
        {
            var database = CsvReader.ReadFromText(smalldatabase);
            var block = database.GetBlock(col_start: 2, col_length: 2).ToArray();
            Assert.AreEqual(10, block.Length);
            Assert.AreEqual(2, block[0].Headers.Length);
            Assert.AreEqual("Never", block[0]["C"]);
            Assert.AreEqual("gonna", block[1]["C"]);
            Assert.AreEqual("give", block[2]["C"]);
            Assert.AreEqual("you", block[3]["C"]);
            Assert.AreEqual("up", block[4]["C"]);
            Assert.AreEqual("Never", block[5]["C"]);
            Assert.AreEqual("gonna", block[6]["C"]);
            Assert.AreEqual("let", block[7]["C"]);
            Assert.AreEqual("you", block[8]["C"]);
            Assert.AreEqual("down", block[9]["C"]);
            Assert.AreEqual("15", block[0]["D"]);
            Assert.AreEqual("28", block[1]["D"]);
            Assert.AreEqual("362", block[2]["D"]);
            Assert.AreEqual("3992", block[3]["D"]);
            Assert.AreEqual("65923", block[4]["D"]);
            Assert.AreEqual("64", block[5]["D"]);
            Assert.AreEqual("95", block[6]["D"]);
            Assert.AreEqual("1537", block[7]["D"]);
            Assert.AreEqual("8463", block[8]["D"]);
            Assert.AreEqual("442", block[9]["D"]);
        }
        [TestMethod]
        [TestCategory("GetBlock")]
        public void GetBlockTest_3()
        {
            var database = CsvReader.ReadFromText(smalldatabase);
            var block = database.GetBlock(row_start: 4, row_length: 3, col_start: 1, col_length: 2).ToArray();
            Assert.AreEqual(3, block.Length);
            Assert.AreEqual(2, block[0].Headers.Length);
            Assert.AreEqual("96.73", block[0]["B"]);
            Assert.AreEqual("up", block[0]["C"]);
            Assert.AreEqual("73.12", block[1]["B"]);
            Assert.AreEqual("Never", block[1]["C"]);
            Assert.AreEqual("89.64", block[2]["B"]);
            Assert.AreEqual("gonna", block[2]["C"]);
        }

        [TestMethod]
        public async Task ReadFromTextAsyncMatchesSync()
        {
            const string csv = "A,B\n1,2\n3,4";
            var sync = CsvReader.ReadFromText(csv).ToArray();
            var asyncLines = new List<ICsvLine>();
            await foreach (var line in CsvReader.ReadFromTextAsync(csv))
                asyncLines.Add(line);

            Assert.AreEqual(sync.Length, asyncLines.Count);
            for (var i = 0; i < sync.Length; i++)
                CollectionAssert.AreEqual(sync[i].Values, asyncLines[i].Values);
        }

        [TestMethod]
        public async Task ReadFromStreamAsyncMatchesSync()
        {
            const string csv = "A,B\n1,2\n3,4";
            var sync = CsvReader.ReadFromStream(new MemoryStream(Encoding.UTF8.GetBytes(csv))).ToArray();
            var asyncLines = new List<ICsvLine>();
            await foreach (var line in CsvReader.ReadFromStreamAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv))))
                asyncLines.Add(line);

            Assert.AreEqual(sync.Length, asyncLines.Count);
            for (var i = 0; i < sync.Length; i++)
                CollectionAssert.AreEqual(sync[i].Values, asyncLines[i].Values);
        }
    }
}