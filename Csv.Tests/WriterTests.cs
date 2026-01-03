using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        [TestMethod]
        public async Task WriteAsync_IEnumerable_WithHeaders()
        {
            // Test WriteAsync with IEnumerable<string[]> and headers
            var headers = new[] { "A", "B", "C" };
            var lines = new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } };
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, headers, lines);
            var result = writer.ToString();

            Assert.AreEqual($"A,B,C{Environment.NewLine}X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task WriteAsync_IEnumerable_SkipHeaderRow()
        {
            // Test WriteAsync with IEnumerable<string[]> and skipHeaderRow
            var lines = new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } };
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, null, lines, ',', skipHeaderRow: true);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task WriteAsync_IEnumerable_ConvenienceOverload()
        {
            // Test WriteAsync convenience overload (no headers)
            var lines = new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } };
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, lines);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task WriteAsync_IEnumerable_WithCustomSeparator()
        {
            // Test WriteAsync with custom separator
            var headers = new[] { "A", "B" };
            var lines = new[] { new[] { "X", "Y" } };
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, headers, lines, ';');
            var result = writer.ToString();

            Assert.AreEqual($"A;B{Environment.NewLine}X;Y{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task WriteAsync_IEnumerable_EscapedValues()
        {
            // Test WriteAsync with values requiring escaping
            var headers = new[] { "A", "B" };
            var lines = new[] { new[] { "X,Y", "\"Z\"" } };
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, headers, lines);
            var result = writer.ToString();

            Assert.AreEqual($"A,B{Environment.NewLine}\"X,Y\",\"\"\"Z\"\"\"{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task WriteAsync_IEnumerable_EmptyLines()
        {
            // Test WriteAsync with empty data
            var headers = new[] { "A", "B" };
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, headers, Array.Empty<string[]>());
            var result = writer.ToString();

            Assert.AreEqual($"A,B{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task WriteAsync_IEnumerable_CancellationToken()
        {
            // Test that cancellation token is respected
            var lines = new[] { new[] { "X", "Y" }, new[] { "1", "2" }, new[] { "A", "B" } };
            var writer = new StringWriter();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await CsvWriter.WriteAsync(writer, null, lines, ',', skipHeaderRow: true, cts.Token));
        }

        [TestMethod]
        public void WriteAsync_IEnumerable_ThrowsWhenHeadersNull()
        {
            // Test that ArgumentNullException is thrown when headers are null and skipHeaderRow is false
            var lines = new[] { new[] { "X", "Y" } };
            var writer = new StringWriter();

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await CsvWriter.WriteAsync(writer, null, lines));
        }

        [TestMethod]
        public async Task WriteAsync_IEnumerable_NullCellValues()
        {
            // Test WriteAsync with null cell values
            var headers = new[] { "A", "B" };
            var lines = new[] { new[] { "X", null! }, new[] { null!, "Y" } };
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, headers, lines);
            var result = writer.ToString();

            Assert.AreEqual($"A,B{Environment.NewLine}X,{Environment.NewLine},Y{Environment.NewLine}", result);
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
        public async Task WriteAsync_SkipHeaderRow_WithNullHeaders()
        {
            // Test async version with null headers
            var lines = AsyncEnumerable(new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } });
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, (string[]?)null, lines, ',', skipHeaderRow: true);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task WriteAsync_SkipHeaderRow_WithNullHeaders_ReadOnlyMemory()
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
        public async Task ConvenienceOverload_WriteAsync()
        {
            // Test async convenience overload
            var lines = AsyncEnumerable(new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } });
            var writer = new StringWriter();
            await CsvWriter.WriteAsync(writer, lines);
            var result = writer.ToString();

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task ConvenienceOverload_WriteToTextAsync()
        {
            // Test async convenience overload
            var lines = AsyncEnumerable(new[] { new[] { "X", "Y", "Z" }, new[] { "1", "2", "3" } });
            var result = await CsvWriter.WriteToTextAsync(lines);

            Assert.AreEqual($"X,Y,Z{Environment.NewLine}1,2,3{Environment.NewLine}", result);
        }

        [TestMethod]
        public async Task ConvenienceOverload_WriteAsync_ReadOnlyMemory()
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
        public async Task ConvenienceOverload_WriteToTextAsync_ReadOnlyMemory()
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
                await Task.Yield();
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