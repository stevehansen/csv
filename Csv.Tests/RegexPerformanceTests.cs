#if NET8_0_OR_GREATER

using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class RegexPerformanceTests
    {
        [TestMethod]
        public void Performance_RegexElimination_Benchmark()
        {
            var options = new CsvOptions { AllowBackSlashToEscapeQuote = true };

            // Test data with various patterns
            var testCases = new[]
            {
                "\"simple quote\"",
                "\"unterminated quote",
                "\"escaped quote\\\"\"",
                "\"multiple\"\"quotes\"",
                "\"complex\\\\\\\"pattern\"",
                "\"" + new string('a', 100) + "\"",
                "\"" + new string('\\', 20) + "\"",
                "'single quote'",
                "'unterminated single",
                "'escaped\\'quote'",
            };

            const int iterations = 10000;

            // Warm up
            foreach (var testCase in testCases)
            {
                CsvLineSplitter.IsUnterminatedQuotedValue(testCase, options);
            }

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                foreach (var testCase in testCases)
                {
                    CsvLineSplitter.IsUnterminatedQuotedValue(testCase, options);
                }
            }
            sw.Stop();

            Console.WriteLine($"Character-based implementation: {sw.ElapsedMilliseconds}ms for {iterations * testCases.Length} operations");
            Console.WriteLine($"Average per operation: {(double)sw.ElapsedMilliseconds / (iterations * testCases.Length):F4}ms");

            // Performance should be reasonable
            Assert.IsLessThan(1000, sw.ElapsedMilliseconds, $"Performance test took too long: {sw.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public void Memory_AllocationTest()
        {
            var options = new CsvOptions { AllowBackSlashToEscapeQuote = true };

            // Warm up to eliminate JIT compilation overhead from measurements
            for (int i = 0; i < 100; i++)
            {
                CsvLineSplitter.IsUnterminatedQuotedValue("\"test quote\"", options);
                CsvLineSplitter.IsUnterminatedQuotedValue("\"unterminated", options);
                CsvLineSplitter.IsUnterminatedQuotedValue("\"escaped\\\\\"\"", options);
            }

            // Force garbage collection before test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var beforeMemory = GC.GetTotalMemory(true);

            // Run operations that should not allocate much memory
            for (int i = 0; i < 1000; i++)
            {
                CsvLineSplitter.IsUnterminatedQuotedValue("\"test quote\"", options);
                CsvLineSplitter.IsUnterminatedQuotedValue("\"unterminated", options);
                CsvLineSplitter.IsUnterminatedQuotedValue("\"escaped\\\"\"", options);
            }

            var afterMemory = GC.GetTotalMemory(false);
            var allocatedMemory = afterMemory - beforeMemory;

            Console.WriteLine($"Memory allocated: {allocatedMemory:N0} bytes");

            // Should allocate minimal memory (allowing tolerance for test infrastructure and GC variability)
            Assert.IsLessThan(500000, allocatedMemory, $"Too much memory allocated: {allocatedMemory:N0} bytes");
        }
    }
}

#endif