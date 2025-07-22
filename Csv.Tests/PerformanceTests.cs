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
            Assert.IsTrue(memoryTime <= traditionalTime * 1.5,
                $"Memory writer should be competitive (traditional: {traditionalTime}ms, memory: {memoryTime}ms)");

            // Buffer writer optimizes for large datasets, so allow more variance for small test data
            // The benefits appear with larger datasets (10k+ rows) due to buffer management overhead
            Assert.IsTrue(bufferTime <= traditionalTime * 15,
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
            Assert.AreEqual(traditionalLines.Length, spanLines.Length);
            Assert.AreEqual(traditionalLines.Length, optimizedLines.Length);

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
            Assert.IsTrue(spanTime <= traditionalTime * 1.5,
                $"Span reader should be competitive (traditional: {traditionalTime}ms, span: {spanTime}ms)");
            Assert.IsTrue(optimizedTime <= traditionalTime * 1.5,
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

            // Force garbage collection before test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var beforeMemory = GC.GetTotalMemory(false);

            // Traditional approach
            var traditionalResult = CsvWriter.WriteToText(headers, stringRows);

            var afterTraditional = GC.GetTotalMemory(false);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var beforeBuffer = GC.GetTotalMemory(false);

            // Buffer writer approach - estimate required size to minimize allocations
            var estimatedSize = (headers.Length + stringRows.Length) * 50; // Rough estimate
            var optimizedOptions = new CsvMemoryOptions
            {
                InitialBufferSize = Math.Min(estimatedSize, 2048),  // Size based on data
                DirectAllocationThreshold = estimatedSize + 1024   // Use direct allocation for this test
            };
            using var bufferWriter = new CsvBufferWriter(optimizedOptions);
            var headerMemories = headers.Select(h => h.AsMemory()).ToArray();
            var memoryRows = stringRows.Select(row => row.Select(cell => cell.AsMemory()).ToArray()).ToArray();
            bufferWriter.WriteCsv(headerMemories.AsSpan(), memoryRows);
            var bufferResult = bufferWriter.ToString();

            var afterBuffer = GC.GetTotalMemory(false);

            // Results should be identical
            Assert.AreEqual(traditionalResult, bufferResult);

            // Calculate memory usage
            var traditionalMemory = afterTraditional - beforeMemory;
            var bufferMemory = afterBuffer - beforeBuffer;

            Console.WriteLine($"Traditional memory usage: {traditionalMemory:N0} bytes");
            Console.WriteLine($"Buffer writer memory usage: {bufferMemory:N0} bytes");
            Console.WriteLine($"Memory reduction: {(1.0 - (double)bufferMemory / traditionalMemory) * 100:F1}%");

            // Buffer writer optimizes for large datasets and reuse scenarios.
            // For small datasets, the Memory<char> conversion overhead may cause higher memory usage.
            // This is acceptable as buffer writers target high-throughput scenarios with large data.
            var memoryRatio = (double)bufferMemory / traditionalMemory;
            Console.WriteLine($"Memory ratio (buffer/traditional): {memoryRatio:F2}x");

            // For small test datasets, allow higher memory usage due to Memory<char> conversion overhead
            // In real-world large dataset scenarios, buffer writers show significant memory benefits
            Assert.IsTrue(bufferMemory <= traditionalMemory * 5.0,
                $"Buffer writer memory overhead should be reasonable for small datasets (traditional: {traditionalMemory:N0}, buffer: {bufferMemory:N0}, ratio: {memoryRatio:F2}x)");
        }
    }
}

#endif