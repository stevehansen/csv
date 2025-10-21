#if NET8_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class SpanMemoryTests
    {
        [TestMethod]
        public void CsvWriter_WriteMemory_BasicFunctionality()
        {
            var headers = new[] { "Name".AsMemory(), "Age".AsMemory(), "City".AsMemory() };
            var lines = new[]
            {
                new[] { "John".AsMemory(), "25".AsMemory(), "NYC".AsMemory() },
                ["Jane".AsMemory(), "30".AsMemory(), "LA".AsMemory()]
            };

            var result = CsvWriter.WriteToText(headers, lines);
            var expected = $"Name,Age,City{Environment.NewLine}John,25,NYC{Environment.NewLine}Jane,30,LA{Environment.NewLine}";

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CsvWriter_WriteMemorySpan_BasicFunctionality()
        {
            var headers = new[] { "Name".AsMemory(), "Age".AsMemory(), "City".AsMemory() };
            var lines = new[]
            {
                new[] { "John".AsMemory(), "25".AsMemory(), "NYC".AsMemory() },
                ["Jane".AsMemory(), "30".AsMemory(), "LA".AsMemory()]
            };

            using var writer = new StringWriter();
            CsvWriter.Write(writer, headers.AsSpan(), lines);

            var result = writer.ToString();
            var expected = $"Name,Age,City{Environment.NewLine}John,25,NYC{Environment.NewLine}Jane,30,LA{Environment.NewLine}";

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CsvWriter_WriteToBuffer_BasicFunctionality()
        {
            var headers = new[] { "A".AsMemory(), "B".AsMemory() };
            var lines = new[] { new[] { "1".AsMemory(), "2".AsMemory() } };

            var buffer = new char[100];
            var success = CsvWriter.WriteToBuffer(buffer, headers, lines, ',', out var written);

            Assert.IsTrue(success);
            Assert.IsGreaterThan(0, written);

            var result = new string(buffer, 0, written);
            Assert.AreEqual("A,B\n1,2\n", result);
        }

        [TestMethod]
        public void CsvWriter_WriteToBuffer_InsufficientBuffer()
        {
            var headers = new[] { "VeryLongHeaderName".AsMemory(), "AnotherLongHeader".AsMemory() };
            var lines = new[] { new[] { "VeryLongValue1".AsMemory(), "VeryLongValue2".AsMemory() } };

            var buffer = new char[10]; // Too small
            var success = CsvWriter.WriteToBuffer(buffer, headers, lines, ',', out _);

            Assert.IsFalse(success);
        }

        [TestMethod]
        public async Task CsvWriter_WriteAsyncMemory_BasicFunctionality()
        {
            var headers = new[] { "Name".AsMemory(), "Age".AsMemory() };
            var lines = GenerateAsyncLines();

            var result = await CsvWriter.WriteToTextAsync(headers, lines);
            var expected = $"Name,Age{Environment.NewLine}John,25{Environment.NewLine}Jane,30{Environment.NewLine}";

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CsvReader_ReadAsSpan_BasicFunctionality()
        {
            var csv = "Name,Age\nJohn,25\nJane,30";

            var lines = CsvReader.ReadFromTextAsSpan(csv).ToArray();

            Assert.HasCount(2, lines);
            Assert.AreEqual("John", lines[0].GetSpan("Name").ToString());
            Assert.AreEqual("25", lines[0].GetSpan("Age").ToString());
            Assert.AreEqual("Jane", lines[1].GetSpan("Name").ToString());
            Assert.AreEqual("30", lines[1].GetSpan("Age").ToString());
        }

        [TestMethod]
        public void CsvReader_ReadAsSpan_MemoryAccess()
        {
            var csv = "Name,Age\nJohn,25\nJane,30";

            var lines = CsvReader.ReadFromTextAsSpan(csv).ToArray();

            Assert.HasCount(2, lines);
            Assert.AreEqual("John", lines[0].GetMemory("Name").ToString());
            Assert.AreEqual("25", lines[0].GetMemory("Age").ToString());
            Assert.HasCount(2, lines[0].HeadersMemory);
            Assert.HasCount(2, lines[0].ValuesMemory);
        }

        [TestMethod]
        public void CsvReader_ReadAsSpan_TryGet()
        {
            var csv = "Name,Age\nJohn,25";

            var line = CsvReader.ReadFromTextAsSpan(csv).First();

            // Test TryGetMemory
            Assert.IsTrue(line.TryGetMemory("Name", out var nameMemory));
            Assert.AreEqual("John", nameMemory.ToString());

            Assert.IsFalse(line.TryGetMemory("NonExistent", out var _));

            // Test TryGetSpan
            Assert.IsTrue(line.TryGetSpan("Age", out var ageSpan));
            Assert.AreEqual("25", ageSpan.ToString());

            Assert.IsFalse(line.TryGetSpan("NonExistent", out var _));
        }

        [TestMethod]
        public void CsvReader_ReadAsSpan_IndexAccess()
        {
            var csv = "Name,Age\nJohn,25";

            var line = CsvReader.ReadFromTextAsSpan(csv).First();

            Assert.AreEqual("John", line.GetSpan(0).ToString());
            Assert.AreEqual("25", line.GetSpan(1).ToString());
            Assert.AreEqual("John", line.GetMemory(0).ToString());
            Assert.AreEqual("25", line.GetMemory(1).ToString());
        }

        [TestMethod]
        public void CsvReader_ReadAsSpan_BackwardCompatibility()
        {
            var csv = "Name,Age\nJohn,25";

            var line = CsvReader.ReadFromTextAsSpan(csv).First();

            // Test that ICsvLine interface still works
            ICsvLine csvLine = line;
            Assert.AreEqual("John", csvLine["Name"]);
            Assert.AreEqual("25", csvLine["Age"]);
            Assert.AreEqual("John", csvLine[0]);
            Assert.AreEqual("25", csvLine[1]);
        }

        [TestMethod]
        public void CsvWriter_EscapingWithMemory()
        {
            var headers = new[] { "Name".AsMemory(), "Description".AsMemory() };
            var lines = new[]
            {
                new[] { "John,Doe".AsMemory(), "A \"quoted\" value".AsMemory() }
            };

            var result = CsvWriter.WriteToText(headers, lines);
            var expected = $"Name,Description{Environment.NewLine}\"John,Doe\",\"A \"\"quoted\"\" value\"{Environment.NewLine}";

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CsvBufferWriter_BasicFunctionality()
        {
            using var writer = new CsvBufferWriter();

            var headers = new[] { "Name".AsMemory(), "Age".AsMemory() };
            var rows = new[]
            {
                new[] { "John".AsMemory(), "25".AsMemory() },
                ["Jane".AsMemory(), "30".AsMemory()]
            };

            writer.WriteCsv(headers, rows);

            var result = writer.ToString();
            var expected = $"Name,Age{Environment.NewLine}John,25{Environment.NewLine}Jane,30{Environment.NewLine}";

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CsvBufferWriter_WithEscaping()
        {
            using var writer = new CsvBufferWriter();

            writer.WriteCell("Simple".AsSpan());
            writer.Write(',');
            writer.WriteCell("Needs \"escaping\"".AsSpan());
            writer.Write(',');
            writer.WriteCell("Contains,comma".AsSpan());

            var result = writer.ToString();
            var expected = "Simple,\"Needs \"\"escaping\"\"\",\"Contains,comma\"";

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CsvBufferWriter_CopyTo()
        {
            using var writer = new CsvBufferWriter();

            writer.Write("Test");
            writer.Write(',');
            writer.Write("Data");

            var buffer = new char[20];
            var copied = writer.CopyTo(buffer);

            Assert.AreEqual(9, copied);
            Assert.AreEqual("Test,Data", new string(buffer, 0, copied));
        }

        [TestMethod]
        public void CsvReader_ReadFromMemoryOptimized()
        {
            var csv = "Name,Age\nJohn,25\nJane,30".AsMemory();
            var memoryOptions = new CsvMemoryOptions { InitialBufferSize = 1024 };

            var lines = CsvReader.ReadFromMemoryOptimized(csv, null, memoryOptions).ToArray();

            Assert.HasCount(2, lines);
            Assert.AreEqual("John", lines[0].GetSpan("Name").ToString());
            Assert.AreEqual("25", lines[0].GetSpan("Age").ToString());
            Assert.AreEqual("Jane", lines[1].GetSpan("Name").ToString());
            Assert.AreEqual("30", lines[1].GetSpan("Age").ToString());
        }

        [TestMethod]
        public void CsvReader_CreateBufferWriter()
        {
            var headers = new[] { "Col1", "Col2" };
            using var writer = CsvReader.CreateBufferWriter(headers);

            writer.WriteRow(["A".AsMemory(), "B".AsMemory()]);
            writer.WriteRow(["C".AsMemory(), "D".AsMemory()]);

            var result = writer.ToString();
            var expected = $"Col1,Col2{Environment.NewLine}A,B{Environment.NewLine}C,D{Environment.NewLine}";

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CsvMemoryOptions_Validation()
        {
            var options = new CsvMemoryOptions
            {
                InitialBufferSize = -1
            };

            Assert.Throws<ArgumentException>(() => options.Validate());

            options.InitialBufferSize = 1024;
            options.MaxBufferSize = 512; // Smaller than initial

            Assert.Throws<ArgumentException>(() => options.Validate());

            options.MaxBufferSize = 2048;
            options.Validate(); // Should not throw
        }

        private static async IAsyncEnumerable<ReadOnlyMemory<char>[]> GenerateAsyncLines()
        {
            await Task.Delay(1);
            yield return new[] { "John".AsMemory(), "25".AsMemory() };
            await Task.Delay(1);
            yield return new[] { "Jane".AsMemory(), "30".AsMemory() };
        }
    }
}

#endif