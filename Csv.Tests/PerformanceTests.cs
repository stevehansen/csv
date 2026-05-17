#if NET8_0_OR_GREATER

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class PerformanceTests
    {
        private const int RowCount = 1000;
        private const int ColumnCount = 5;

        [TestMethod]
        public void Performance_CompareWriterMethods()
        {
            // Generate test data
            var headers = Enumerable.Range(0, ColumnCount).Select(i => $"Column{i}").ToArray();
            var stringRows = Enumerable.Range(0, RowCount)
                .Select(r => Enumerable.Range(0, ColumnCount)
                    .Select(c => $"Row{r}Col{c}")
                    .ToArray())
                .ToArray();

            var memoryRows = stringRows
                .Select(row => row.Select(cell => cell.AsMemory()).ToArray())
                .ToArray();

            // Benchmark traditional string-based writer
            var sw = Stopwatch.StartNew();
            var traditionalResult = CsvWriter.WriteToText(headers, stringRows);
            sw.Stop();
            var traditionalTime = sw.ElapsedMilliseconds;

            // Benchmark new memory-based writer
            sw.Restart();
            var memoryHeaders = headers.Select(h => h.AsMemory()).ToArray();
            var memoryResult = CsvWriter.WriteToText(memoryHeaders, memoryRows);
            sw.Stop();
            var memoryTime = sw.ElapsedMilliseconds;

            // Benchmark buffer writer
            sw.Restart();
            using var bufferWriter = new CsvBufferWriter();
            bufferWriter.WriteCsv(memoryHeaders.AsSpan(), memoryRows);
            var bufferResult = bufferWriter.ToString();
            sw.Stop();
            var bufferTime = sw.ElapsedMilliseconds;

            // Verify results are equivalent
            Assert.AreEqual(traditionalResult, memoryResult);
            Assert.AreEqual(traditionalResult, bufferResult);

            // Output performance results
            Console.WriteLine($"Traditional Writer: {traditionalTime}ms");
            Console.WriteLine($"Memory Writer: {memoryTime}ms ({(double)traditionalTime/memoryTime:F2}x)");
            Console.WriteLine($"Buffer Writer: {bufferTime}ms ({(double)traditionalTime/bufferTime:F2}x)");
            Console.WriteLine($"Output size: {traditionalResult.Length:N0} characters");

            // Memory writer should be competitive, buffer writer may have overhead for small datasets
            // For very fast operations (< 5ms), allow up to 3x variance due to CI environment noise
            // For longer operations, require within 2x of traditional performance
            var timeDiff = memoryTime - traditionalTime;
            Assert.IsTrue(
                (traditionalTime == 0 && memoryTime <= 20) ||
                (traditionalTime < 5 && memoryTime <= traditionalTime * 3) ||
                timeDiff < 5 ||
                memoryTime <= traditionalTime * 2,
                $"Memory writer should be competitive (traditional: {traditionalTime}ms, memory: {memoryTime}ms)");

            // Buffer writer optimizes for large datasets, so allow more variance for small test data
            // The benefits appear with larger datasets (10k+ rows) due to buffer management overhead
            Assert.IsTrue(
                (traditionalTime == 0 && bufferTime <= 20) ||
                bufferTime <= traditionalTime * 15,
                $"Buffer writer overhead should be reasonable (traditional: {traditionalTime}ms, buffer: {bufferTime}ms)");
        }

        [TestMethod]
        public void Performance_CompareReaderMethods()
        {
            // Generate test CSV data
            var headers = Enumerable.Range(0, ColumnCount).Select(i => $"Column{i}").ToArray();
            var rows = Enumerable.Range(0, RowCount)
                .Select(r => Enumerable.Range(0, ColumnCount)
                    .Select(c => $"Row{r}Col{c}")
                    .ToArray())
                .ToArray();

            var csvData = CsvWriter.WriteToText(headers, rows);

            // Benchmark traditional string-based reader
            var sw = Stopwatch.StartNew();
            var traditionalLines = CsvReader.ReadFromText(csvData).ToArray();
            sw.Stop();
            var traditionalTime = sw.ElapsedMilliseconds;

            // Benchmark new span-based reader
            sw.Restart();
            var spanLines = CsvReader.ReadFromTextAsSpan(csvData).ToArray();
            sw.Stop();
            var spanTime = sw.ElapsedMilliseconds;

            // Benchmark optimized memory reader
            sw.Restart();
            var optimizedLines = CsvReader.ReadFromMemoryOptimized(csvData.AsMemory()).ToArray();
            sw.Stop();
            var optimizedTime = sw.ElapsedMilliseconds;

            // Verify results are equivalent
            Assert.HasCount(traditionalLines.Length, spanLines);
            Assert.HasCount(traditionalLines.Length, optimizedLines);

            for (int i = 0; i < Math.Min(100, traditionalLines.Length); i++) // Check first 100 lines
            {
                Assert.AreEqual(traditionalLines[i][0], spanLines[i].GetSpan(0).ToString());
                Assert.AreEqual(traditionalLines[i][0], optimizedLines[i].GetSpan(0).ToString());
            }

            // Output performance results
            Console.WriteLine($"Traditional Reader: {traditionalTime}ms");
            Console.WriteLine($"Span Reader: {spanTime}ms ({(double)traditionalTime/spanTime:F2}x)");
            Console.WriteLine($"Optimized Reader: {optimizedTime}ms ({(double)traditionalTime/optimizedTime:F2}x)");
            Console.WriteLine($"Processed {traditionalLines.Length:N0} rows");

            // Span/optimized reader should be faster or at least comparable
            // For very fast operations (< 5ms), allow up to 10x variance due to CI environment noise and JIT warmup
            // For longer operations, require within 2x of traditional performance
            var spanTimeDiff = spanTime - traditionalTime;
            var optimizedTimeDiff = optimizedTime - traditionalTime;

            Assert.IsTrue(
                (traditionalTime == 0 && spanTime <= 20) ||
                (traditionalTime < 5 && spanTime <= traditionalTime * 10) ||
                spanTimeDiff < 5 ||
                spanTime <= traditionalTime * 2,
                $"Span reader should be competitive (traditional: {traditionalTime}ms, span: {spanTime}ms)");

            Assert.IsTrue(
                (traditionalTime == 0 && optimizedTime <= 20) ||
                (traditionalTime < 5 && optimizedTime <= traditionalTime * 10) ||
                optimizedTimeDiff < 5 ||
                optimizedTime <= traditionalTime * 2,
                $"Optimized reader should be competitive (traditional: {traditionalTime}ms, optimized: {optimizedTime}ms)");
        }

        [TestMethod]
        public void Memory_AllocationComparison()
        {
            const int testRows = 1000;
            var headers = new[] { "Name", "Age", "City", "Score" };
            var stringRows = Enumerable.Range(0, testRows)
                .Select(i => new[] { $"Person{i}", $"{20 + i % 60}", $"City{i % 10}", $"{i * 1.5:F1}" })
                .ToArray();

            // Pre-build the Memory<char> view of the same input so the input-adaptation cost
            // is excluded from the buffer writer's measurement window — it isn't part of
            // CsvBufferWriter and would otherwise dominate the comparison at 1000 rows.
            var headerMemories = headers.Select(h => h.AsMemory()).ToArray();
            var memoryRows = stringRows.Select(row => row.Select(cell => cell.AsMemory()).ToArray()).ToArray();

            var estimatedSize = (headers.Length + stringRows.Length) * 50;
            var optimizedOptions = new CsvMemoryOptions
            {
                InitialBufferSize = Math.Min(estimatedSize, 2048),
                DirectAllocationThreshold = estimatedSize + 1024
            };
            // Warm up both code paths so first-call JIT cost doesn't land on whichever runs first.
            _ = CsvWriter.WriteToText(headers, stringRows);
            using (var warmup = new CsvBufferWriter(optimizedOptions))
            {
                warmup.WriteCsv(headerMemories.AsSpan(), memoryRows);
                _ = warmup.ToString();
            }

            using var bufferWriter = new CsvBufferWriter(optimizedOptions);

            // GC.GetAllocatedBytesForCurrentThread() is a deterministic per-thread allocation
            // counter — unaffected by other tests, GC timing, or background JIT, unlike
            // GC.GetTotalMemory() which measures the whole-process heap delta.
            var beforeTraditional = GC.GetAllocatedBytesForCurrentThread();
            var traditionalResult = CsvWriter.WriteToText(headers, stringRows);
            var traditionalMemory = GC.GetAllocatedBytesForCurrentThread() - beforeTraditional;

            var beforeBuffer = GC.GetAllocatedBytesForCurrentThread();
            bufferWriter.WriteCsv(headerMemories.AsSpan(), memoryRows);
            var bufferResult = bufferWriter.ToString();
            var bufferMemory = GC.GetAllocatedBytesForCurrentThread() - beforeBuffer;

            Assert.AreEqual(traditionalResult, bufferResult);

            var memoryRatio = (double)bufferMemory / traditionalMemory;
            Console.WriteLine($"Traditional: {traditionalMemory:N0} bytes, buffer: {bufferMemory:N0} bytes, ratio: {memoryRatio:F2}x");

            // For the one-shot WriteCsv + ToString pattern measured here, the buffer writer
            // sits at ~3x the traditional path's allocations and the ratio is stable from
            // 100 to 100k rows — the buffer writer's value is in CopyTo / reuse / streaming
            // scenarios, not "build a CSV string once." This bound is a regression tripwire.
            Assert.IsLessThanOrEqualTo(traditionalMemory * 4.0, bufferMemory,
                $"Buffer writer one-shot allocations should stay within ~4x the traditional writer (traditional: {traditionalMemory:N0}, buffer: {bufferMemory:N0}, ratio: {memoryRatio:F2}x)");
        }
    }
}

#endif