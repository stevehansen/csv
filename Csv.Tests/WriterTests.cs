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

        [TestMethod]
        public void SkipHeaderRow_WithHeaders()
        {
            // When skipHeaderRow is true but headers are provided, they should be skipped
            // but the column count should still come from headers
            var headers = new[] { "A", "B", "C" };
            var lines = new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } };
            var writer = new StringWriter();
            CsvWriter.Write(writer, headers, lines, skipHeaderRow: true);
            var result = writer.ToString();

            // Should not contain header row
            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void SkipHeaderRow_WithNullHeaders()
        {
            // When skipHeaderRow is true and headers are null, column count should come from first data line
            var lines = new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } };
            var writer = new StringWriter();
            CsvWriter.Write(writer, null, lines, skipHeaderRow: true);
            var result = writer.ToString();

            // Should write data rows without headers
            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void SkipHeaderRow_WithNullHeaders_EmptyLines()
        {
            // When skipHeaderRow is true, headers are null, and lines are empty
            var writer = new StringWriter();
            CsvWriter.Write(writer, null, Array.Empty<string[]>(), skipHeaderRow: true);
            var result = writer.ToString();

            // Should produce empty output
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void SkipHeaderRow_WithNullHeaders_DifferentColumnCounts()
        {
            // When headers are null, column count comes from first line
            // even if subsequent lines have different column counts
            var lines = new[] { new[] { "X", "Y" }, new[] { "1", "2", "3", "4" } };
            var writer = new StringWriter();
            CsvWriter.Write(writer, null, lines, skipHeaderRow: true);
            var result = writer.ToString();

            // First line determines column count (2)
            Assert.AreEqual($"X,Y{Environment.NewLine}1,2{Environment.NewLine}", result);
        }

        [TestMethod]
        public void ThrowsWhenHeadersNull_AndSkipHeaderRowFalse()
        {
            // When skipHeaderRow is false (default) and headers are null, should throw
            var lines = new[] { new[] { "X", "Y", "Z" } };
            var writer = new StringWriter();

            Assert.Throws<ArgumentNullException>(() =>
                CsvWriter.Write(writer, null, lines));
        }

        [TestMethod]
        public void WriteToText_SkipHeaderRow_WithNullHeaders()
        {
            // Test WriteToText method with null headers
            var lines = new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } };
            var result = CsvWriter.WriteToText(null, lines, skipHeaderRow: true);

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void ConvenienceOverload_Write_WithoutHeaders()
        {
            // Test the convenience overload that doesn't require headers parameter
            var lines = new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } };
            var writer = new StringWriter();
            CsvWriter.Write(writer, lines);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void ConvenienceOverload_WriteToText_WithoutHeaders()
        {
            // Test the convenience overload that doesn't require headers parameter
            var lines = new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } };
            var result = CsvWriter.WriteToText(lines);

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void ConvenienceOverload_WithCustomSeparator()
        {
            // Test convenience overload with custom separator
            var lines = new[] { new[] { "A", "B" }, new[] { "C", "D" } };
            var result = CsvWriter.WriteToText(lines, ';');

            Assert.AreEqual($"A;B{Environment.NewLine}C;D{Environment.NewLine}", result);
        }

        [TestMethod]
        public void ConvenienceOverload_EmptyLines()
        {
            // Test convenience overload with empty data
            var result = CsvWriter.WriteToText(Array.Empty<string[]>());

            Assert.AreEqual(string.Empty, result);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public void SkipHeaderRow_WithNullHeaders_ReadOnlyMemory()
        {
            // Test ReadOnlyMemory overload with null headers
            var lines = new[]
            {
                new[] { "X".AsMemory(), "Y".AsMemory(), "Z".AsMemory() },
                new[] { "1".AsMemory(), "2".AsMemory(), "3".AsMemory() }
            };
            var writer = new StringWriter();
            CsvWriter.Write(writer, null, lines, skipHeaderRow: true);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void SkipHeaderRow_WithEmptyHeaders_ReadOnlySpan()
        {
            // Test ReadOnlySpan overload with empty headers
            var lines = new[]
            {
                new[] { "X".AsMemory(), "Y".AsMemory(), "Z".AsMemory() },
                new[] { "1".AsMemory(), "2".AsMemory(), "3".AsMemory() }
            };
            var writer = new StringWriter();
            ReadOnlySpan<ReadOnlyMemory<char>> emptyHeaders = ReadOnlySpan<ReadOnlyMemory<char>>.Empty;
            CsvWriter.Write(writer, emptyHeaders, lines, skipHeaderRow: true);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void ThrowsWhenHeadersEmpty_AndSkipHeaderRowFalse_ReadOnlySpan()
        {
            // ReadOnlySpan version should throw ArgumentException when empty and skipHeaderRow is false
            var lines = new[] { new[] { "X".AsMemory(), "Y".AsMemory() } };
            var writer = new StringWriter();
            ReadOnlySpan<ReadOnlyMemory<char>> emptyHeaders = ReadOnlySpan<ReadOnlyMemory<char>>.Empty;

            var thrown = false;
            try
            {
                CsvWriter.Write(writer, emptyHeaders, lines);
            }
            catch (ArgumentException)
            {
                thrown = true;
            }

            Assert.IsTrue(thrown, "Expected ArgumentException to be thrown");
        }

        [TestMethod]
        public async System.Threading.Tasks.Task WriteAsync_SkipHeaderRow_WithNullHeaders()
        {
            // Test async version with null headers
            var lines = AsyncEnumerable(new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } });
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, (string[]?)null, lines, ',', skipHeaderRow: true);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task WriteAsync_SkipHeaderRow_WithNullHeaders_ReadOnlyMemory()
        {
            // Test async ReadOnlyMemory version with null headers
            var lines = AsyncEnumerable(new[]
            {
                new[] { "X".AsMemory(), "Y".AsMemory(), "Z".AsMemory() },
                new[] { "1".AsMemory(), "2".AsMemory(), "3".AsMemory() }
            });
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, (ReadOnlyMemory<char>[]?)null, lines, ',', skipHeaderRow: true);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void ConvenienceOverload_Write_ReadOnlyMemory()
        {
            // Test ReadOnlyMemory convenience overload
            var lines = new[]
            {
                new[] { "X".AsMemory(), "Y".AsMemory(), "Z".AsMemory() },
                new[] { "1".AsMemory(), "2".AsMemory(), "3".AsMemory() }
            };
            var writer = new StringWriter();
            CsvWriter.Write(writer, lines);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public void ConvenienceOverload_WriteToText_ReadOnlyMemory()
        {
            // Test ReadOnlyMemory convenience overload
            var lines = new[]
            {
                new[] { "X".AsMemory(), "Y".AsMemory(), "Z".AsMemory() },
                new[] { "1".AsMemory(), "2".AsMemory(), "3".AsMemory() }
            };
            var result = CsvWriter.WriteToText(lines);

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ConvenienceOverload_WriteAsync()
        {
            // Test async convenience overload
            var lines = AsyncEnumerable(new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } });
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, lines);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ConvenienceOverload_WriteToTextAsync()
        {
            // Test async convenience overload
            var lines = AsyncEnumerable(new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } });
            var result = await CsvWriter.WriteToTextAsync(lines);

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ConvenienceOverload_WriteAsync_ReadOnlyMemory()
        {
            // Test async ReadOnlyMemory convenience overload
            var lines = AsyncEnumerable(new[]
            {
                new[] { "X".AsMemory(), "Y".AsMemory(), "Z".AsMemory() },
                new[] { "1".AsMemory(), "2".AsMemory(), "3".AsMemory() }
            });
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, lines);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ConvenienceOverload_WriteToTextAsync_ReadOnlyMemory()
        {
            // Test async ReadOnlyMemory convenience overload
            var lines = AsyncEnumerable(new[]
            {
                new[] { "X".AsMemory(), "Y".AsMemory(), "Z".AsMemory() },
                new[] { "1".AsMemory(), "2".AsMemory(), "3".AsMemory() }
            });
            var result = await CsvWriter.WriteToTextAsync(lines);

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        private static async System.Collections.Generic.IAsyncEnumerable<T> AsyncEnumerable<T>(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                await System.Threading.Tasks.Task.Yield();
                yield return item;
            }
        }
#endif

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