#if NET8_0_OR_GREATER

using System;
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

            // Build the Memory<char> views of the input up front so input-adaptation cost is
            // excluded from the measurement windows below (it isn't part of the writers).
            var memoryHeaders = headers.Select(h => h.AsMemory()).ToArray();
            var memoryRows = stringRows
                .Select(row => row.Select(cell => cell.AsMemory()).ToArray())
                .ToArray();

            // Warm up every path so first-call JIT cost doesn't land on whichever runs first
            // and skew the comparison.
            _ = CsvWriter.WriteToText(headers, stringRows);
            _ = CsvWriter.WriteToText(memoryHeaders, memoryRows);
            using (var warmup = new CsvBufferWriter())
            {
                warmup.WriteCsv(memoryHeaders.AsSpan(), memoryRows);
                _ = warmup.ToString();
            }

            // Compare allocations instead of wall-clock time. GC.GetAllocatedBytesForCurrentThread()
            // is a deterministic per-thread counter — immune to CI noise, test parallelism, and GC
            // timing — whereas the previous Stopwatch millisecond ratios flaked because sub-5ms
            // operations round unpredictably under load.
            var beforeTraditional = GC.GetAllocatedBytesForCurrentThread();
            var traditionalResult = CsvWriter.WriteToText(headers, stringRows);
            var traditionalMemory = GC.GetAllocatedBytesForCurrentThread() - beforeTraditional;

            var beforeMemory = GC.GetAllocatedBytesForCurrentThread();
            var memoryResult = CsvWriter.WriteToText(memoryHeaders, memoryRows);
            var memoryAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeMemory;

            using var bufferWriter = new CsvBufferWriter();
            var beforeBuffer = GC.GetAllocatedBytesForCurrentThread();
            bufferWriter.WriteCsv(memoryHeaders.AsSpan(), memoryRows);
            var bufferResult = bufferWriter.ToString();
            var bufferAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeBuffer;

            // All three writers must produce identical output — the load-bearing correctness
            // check, and fully deterministic.
            Assert.AreEqual(traditionalResult, memoryResult);
            Assert.AreEqual(traditionalResult, bufferResult);

            Console.WriteLine($"Traditional writer: {traditionalMemory:N0} bytes");
            Console.WriteLine($"Memory writer:      {memoryAllocated:N0} bytes ({(double)memoryAllocated / traditionalMemory:F2}x)");
            Console.WriteLine($"Buffer writer:      {bufferAllocated:N0} bytes ({(double)bufferAllocated / traditionalMemory:F2}x)");
            Console.WriteLine($"Output size: {traditionalResult.Length:N0} characters");

            // Deterministic competitiveness tripwire: the Memory<char> WriteToText overload should
            // not allocate dramatically more than the string overload. The bound is generous (a
            // regression backstop, not a fine-grained benchmark) but still catches a reintroduced
            // per-cell allocation. The buffer writer's allocation profile is gated separately by
            // Memory_AllocationComparison.
            Assert.IsLessThanOrEqualTo(traditionalMemory * 2, memoryAllocated,
                $"Memory writer allocations should be competitive with the traditional writer (traditional: {traditionalMemory:N0}, memory: {memoryAllocated:N0})");
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
            var csvMemory = csvData.AsMemory();

            // Warm up every path so first-call JIT cost doesn't land on whichever runs first
            // and skew the comparison.
            _ = CsvReader.ReadFromText(csvData).ToArray();
            _ = CsvReader.ReadFromTextAsSpan(csvData).ToArray();
            _ = CsvReader.ReadFromMemoryOptimized(csvMemory).ToArray();

            // Compare allocations instead of wall-clock time (see Performance_CompareWriterMethods):
            // GC.GetAllocatedBytesForCurrentThread() is a deterministic per-thread counter, so the
            // comparison no longer flakes under test parallelism the way the previous Stopwatch
            // millisecond ratios did.
            var beforeTraditional = GC.GetAllocatedBytesForCurrentThread();
            var traditionalLines = CsvReader.ReadFromText(csvData).ToArray();
            var traditionalMemory = GC.GetAllocatedBytesForCurrentThread() - beforeTraditional;

            var beforeSpan = GC.GetAllocatedBytesForCurrentThread();
            var spanLines = CsvReader.ReadFromTextAsSpan(csvData).ToArray();
            var spanAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeSpan;

            var beforeOptimized = GC.GetAllocatedBytesForCurrentThread();
            var optimizedLines = CsvReader.ReadFromMemoryOptimized(csvMemory).ToArray();
            var optimizedAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeOptimized;

            // All three readers must parse to the same values — the load-bearing correctness
            // check, and fully deterministic.
            Assert.HasCount(traditionalLines.Length, spanLines);
            Assert.HasCount(traditionalLines.Length, optimizedLines);

            for (int i = 0; i < Math.Min(100, traditionalLines.Length); i++) // Check first 100 records
            {
                Assert.AreEqual(traditionalLines[i][0], spanLines[i].GetSpan(0).ToString());
                Assert.AreEqual(traditionalLines[i][0], optimizedLines[i].GetSpan(0).ToString());
            }

            Console.WriteLine($"Traditional reader: {traditionalMemory:N0} bytes");
            Console.WriteLine($"Span reader:        {spanAllocated:N0} bytes ({(double)spanAllocated / traditionalMemory:F2}x)");
            Console.WriteLine($"Optimized reader:   {optimizedAllocated:N0} bytes ({(double)optimizedAllocated / traditionalMemory:F2}x)");
            Console.WriteLine($"Processed {traditionalLines.Length:N0} rows");

            // Deterministic competitiveness tripwire: the span and optimized readers exist to avoid
            // the per-field string copies the traditional reader makes, so neither should allocate
            // dramatically more than the traditional path. The bound is a generous regression
            // backstop, not a fine-grained benchmark.
            Assert.IsLessThanOrEqualTo(traditionalMemory * 2, spanAllocated,
                $"Span reader allocations should be competitive with the traditional reader (traditional: {traditionalMemory:N0}, span: {spanAllocated:N0})");
            Assert.IsLessThanOrEqualTo(traditionalMemory * 2, optimizedAllocated,
                $"Optimized reader allocations should be competitive with the traditional reader (traditional: {traditionalMemory:N0}, optimized: {optimizedAllocated:N0})");
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

            // The buffer writer sits at ~0.75-0.80x the traditional StringBuilder path's
            // allocations and the ratio is stable from 100 to 100k rows. This bound is a
            // regression tripwire — it catches reintroduction of the per-WriteCell
            // SearchValues allocation or the ToString intermediate char[].
            Assert.IsLessThanOrEqualTo(traditionalMemory, bufferMemory,
                $"Buffer writer one-shot allocations should not exceed the traditional writer (traditional: {traditionalMemory:N0}, buffer: {bufferMemory:N0}, ratio: {memoryRatio:F2}x)");
        }
    }
}

#endif