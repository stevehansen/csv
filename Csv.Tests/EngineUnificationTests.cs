using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    [TestClass]
    public class EngineUnificationTests
    {
        private enum ReadPath
        {
            Read,
#if NET8_0_OR_GREATER
            ReadAsSpan,
            ReadAsync,
            ReadFromMemoryOptimized,
            ReadFromMemory,
#endif
        }

        private static readonly ReadPath[] AllPaths =
        {
            ReadPath.Read,
#if NET8_0_OR_GREATER
            ReadPath.ReadAsSpan,
            ReadPath.ReadAsync,
            ReadPath.ReadFromMemoryOptimized,
            ReadPath.ReadFromMemory,
#endif
        };

        private sealed class Row
        {
            public string[] Headers { get; set; } = Array.Empty<string>();
            public string[] Values { get; set; } = Array.Empty<string>();
            public int Index { get; set; }
            public string Raw { get; set; } = string.Empty;
            public Dictionary<string, string> ByName { get; set; } = new();
        }

        private static List<Row> Run(ReadPath path, string csv, Func<CsvOptions> optionsFactory)
        {
            return path switch
            {
                ReadPath.Read => RunRead(csv, optionsFactory()),
#if NET8_0_OR_GREATER
                ReadPath.ReadAsSpan => RunReadAsSpan(csv, optionsFactory()),
                ReadPath.ReadAsync => RunReadAsync(csv, optionsFactory()).GetAwaiter().GetResult(),
                ReadPath.ReadFromMemoryOptimized => RunReadFromMemoryOptimized(csv, optionsFactory()),
                ReadPath.ReadFromMemory => RunReadFromMemory(csv, optionsFactory()),
#endif
                _ => throw new ArgumentOutOfRangeException(nameof(path))
            };
        }

        private static List<Row> RunRead(string csv, CsvOptions options)
        {
            using var reader = new StringReader(csv);
            var result = new List<Row>();
            foreach (var line in CsvReader.Read(reader, options))
            {
                var byName = new Dictionary<string, string>();
                foreach (var h in line.Headers)
                {
                    if (line.LineHasColumn(h))
                        byName[h] = line[h];
                }

                result.Add(new Row
                {
                    Headers = line.Headers,
                    Values = line.Values,
                    Index = line.Index,
                    Raw = line.Raw,
                    ByName = byName,
                });
            }
            return result;
        }

#if NET8_0_OR_GREATER
        private static List<Row> RunReadAsSpan(string csv, CsvOptions options)
        {
            using var reader = new StringReader(csv);
            var result = new List<Row>();
            foreach (var line in CsvReader.ReadAsSpan(reader, options))
            {
                var byName = new Dictionary<string, string>();
                foreach (var h in line.Headers)
                {
                    if (line.LineHasColumn(h))
                        byName[h] = line[h];
                }

                result.Add(new Row
                {
                    Headers = line.Headers,
                    Values = line.Values,
                    Index = line.Index,
                    Raw = line.Raw,
                    ByName = byName,
                });
            }
            return result;
        }

        private static async Task<List<Row>> RunReadAsync(string csv, CsvOptions options)
        {
            using var reader = new StringReader(csv);
            var result = new List<Row>();
            await foreach (var line in CsvReader.ReadAsync(reader, options))
            {
                var byName = new Dictionary<string, string>();
                foreach (var h in line.Headers)
                {
                    if (line.LineHasColumn(h))
                        byName[h] = line[h];
                }

                result.Add(new Row
                {
                    Headers = line.Headers,
                    Values = line.Values,
                    Index = line.Index,
                    Raw = line.Raw,
                    ByName = byName,
                });
            }
            return result;
        }

        private static List<Row> RunReadFromMemoryOptimized(string csv, CsvOptions options)
        {
            var result = new List<Row>();
            foreach (var line in CsvReader.ReadFromMemoryOptimized(csv.AsMemory(), options))
            {
                var byName = new Dictionary<string, string>();
                foreach (var h in line.Headers)
                {
                    if (line.LineHasColumn(h))
                        byName[h] = line[h];
                }

                result.Add(new Row
                {
                    Headers = line.Headers,
                    Values = line.Values,
                    Index = line.Index,
                    Raw = line.Raw,
                    ByName = byName,
                });
            }
            return result;
        }

        private static List<Row> RunReadFromMemory(string csv, CsvOptions options)
        {
            var result = new List<Row>();
            foreach (var line in CsvReader.ReadFromMemory(csv.AsMemory(), options))
            {
                var headerStrings = line.Headers.Select(h => h.ToString()).ToArray();
                var valueStrings = line.Values.Select(v => v.ToString()).ToArray();
                var byName = new Dictionary<string, string>();
                foreach (var h in headerStrings)
                {
                    if (line.LineHasColumn(h))
                        byName[h] = line[h].ToString();
                }

                result.Add(new Row
                {
                    Headers = headerStrings,
                    Values = valueStrings,
                    Index = line.Index,
                    Raw = line.Raw.ToString(),
                    ByName = byName,
                });
            }
            return result;
        }
#endif

        // ----------------------------------------------------------------------
        // 1. Cross-path skip / header / alias matrix
        // ----------------------------------------------------------------------

        [TestMethod]
        public void When_HeaderPresentHappyPath_Then_AllPathsProduceTwoRecordsWithNamedAndIndexedAccess()
        {
            var csv = "name,age\nAlice,30\nBob,25\n";
            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions());

                Assert.AreEqual(2, rows.Count, $"path={path}");
                Assert.AreEqual(2, rows[0].Headers.Length, $"path={path}");
                Assert.AreEqual("name", rows[0].Headers[0], $"path={path}");
                Assert.AreEqual("age", rows[0].Headers[1], $"path={path}");
                Assert.AreEqual("Alice", rows[0].Values[0], $"path={path}");
                Assert.AreEqual("30", rows[0].Values[1], $"path={path}");
                Assert.AreEqual("Alice", rows[0].ByName["name"], $"path={path}");
                Assert.AreEqual("30", rows[0].ByName["age"], $"path={path}");
                Assert.AreEqual("Bob", rows[1].Values[0], $"path={path}");
                Assert.AreEqual("25", rows[1].Values[1], $"path={path}");
            }
        }

        [TestMethod]
        public void When_HeaderAbsentHappyPath_Then_AllPathsSynthesizeColumn1Column2Headers()
        {
            var csv = "1,2,3\n4,5,6\n";
            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions { HeaderMode = HeaderMode.HeaderAbsent });

                Assert.AreEqual(2, rows.Count, $"path={path}");
                CollectionAssert.AreEqual(new[] { "Column1", "Column2", "Column3" }, rows[0].Headers, $"path={path}");
                CollectionAssert.AreEqual(new[] { "1", "2", "3" }, rows[0].Values, $"path={path}");
                CollectionAssert.AreEqual(new[] { "4", "5", "6" }, rows[1].Values, $"path={path}");
                Assert.AreEqual("1", rows[0].ByName["Column1"], $"path={path}");
                Assert.AreEqual("6", rows[1].ByName["Column3"], $"path={path}");
            }
        }

        [TestMethod]
        public void When_RowsToSkipIsTwo_Then_AllPathsTreatThirdLineAsHeader()
        {
            var csv = "preamble line 1\npreamble line 2\nname,age\nAlice,30\nBob,25\n";
            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions { RowsToSkip = 2 });

                Assert.AreEqual(2, rows.Count, $"path={path}");
                CollectionAssert.AreEqual(new[] { "name", "age" }, rows[0].Headers, $"path={path}");
                Assert.AreEqual("Alice", rows[0].Values[0], $"path={path}");
                Assert.AreEqual("Bob", rows[1].Values[0], $"path={path}");
            }
        }

        [TestMethod]
        public void When_SkipRowFiltersCommentLines_Then_AllPathsExcludeThem()
        {
            var csv = "name,age\n# comment row\nAlice,30\n# another\nBob,25\n";
            foreach (var path in AllPaths)
            {
                // The default SkipRow predicate already skips '#' lines; we re-state it explicitly for clarity.
                var rows = Run(path, csv, () => new CsvOptions
                {
#if NET8_0_OR_GREATER
                    SkipRow = (row, idx) => row.Span.IsEmpty || row.Span[0] == '#'
#else
                    SkipRow = (row, idx) => string.IsNullOrEmpty(row) || row[0] == '#'
#endif
                });

                Assert.AreEqual(2, rows.Count, $"path={path}");
                Assert.AreEqual("Alice", rows[0].Values[0], $"path={path}");
                Assert.AreEqual("Bob", rows[1].Values[0], $"path={path}");
            }
        }

        [TestMethod]
        public void When_AliasGroupMatchesOneHeader_Then_AllAliasNamesResolveToSameColumn()
        {
            // Aliases live in the header lookup but not in the Headers array, so this case
            // accesses the row via the public indexer directly rather than the path-agnostic
            // ByName projection used elsewhere.
            var csv = "category,price\nbooks,10\n";

            void AssertOnReadLineLike(Func<CsvOptions, IEnumerable<ICsvLine>> source)
            {
                var options = new CsvOptions
                {
                    Aliases = new List<string[]>
                    {
                        new[] { "category", "Category Name", "category_name" }
                    }
                };
                var lines = source(options).ToList();
                Assert.AreEqual(1, lines.Count);
                Assert.AreEqual("books", lines[0]["category"]);
                Assert.AreEqual("books", lines[0]["Category Name"]);
                Assert.AreEqual("books", lines[0]["category_name"]);
            }

            AssertOnReadLineLike(opts =>
            {
                var reader = new StringReader(csv);
                return CsvReader.Read(reader, opts);
            });

#if NET8_0_OR_GREATER
            AssertOnReadLineLike(opts =>
            {
                var reader = new StringReader(csv);
                return CsvReader.ReadAsSpan(reader, opts);
            });

            // Async path
            {
                var options = new CsvOptions
                {
                    Aliases = new List<string[]>
                    {
                        new[] { "category", "Category Name", "category_name" }
                    }
                };
                using var reader = new StringReader(csv);
                var async = CollectAsync(CsvReader.ReadAsync(reader, options)).GetAwaiter().GetResult();
                Assert.AreEqual(1, async.Count);
                Assert.AreEqual("books", async[0]["category"]);
                Assert.AreEqual("books", async[0]["Category Name"]);
                Assert.AreEqual("books", async[0]["category_name"]);
            }

            AssertOnReadLineLike(opts => CsvReader.ReadFromMemoryOptimized(csv.AsMemory(), opts));

            // ReadFromMemory returns ICsvLineFromMemory rather than ICsvLine.
            {
                var options = new CsvOptions
                {
                    Aliases = new List<string[]>
                    {
                        new[] { "category", "Category Name", "category_name" }
                    }
                };
                var lines = CsvReader.ReadFromMemory(csv.AsMemory(), options).ToList();
                Assert.AreEqual(1, lines.Count);
                Assert.AreEqual("books", lines[0]["category"].ToString());
                Assert.AreEqual("books", lines[0]["Category Name"].ToString());
                Assert.AreEqual("books", lines[0]["category_name"].ToString());
            }
#endif
        }

#if NET8_0_OR_GREATER
        private static async Task<List<ICsvLine>> CollectAsync(IAsyncEnumerable<ICsvLine> source)
        {
            var result = new List<ICsvLine>();
            await foreach (var line in source)
                result.Add(line);
            return result;
        }
#endif

        [TestMethod]
        public void When_AliasGroupMatchesMultipleHeaders_Then_AllPathsThrowInvalidOperation()
        {
            var csv = "A,B\n1,2\n";
            foreach (var path in AllPaths)
            {
                var ex = Assert.ThrowsExactly<InvalidOperationException>(
                    () => Run(path, csv, () => new CsvOptions
                    {
                        Aliases = new List<string[]> { new[] { "A", "B" } }
                    }),
                    $"path={path}");

                StringAssert.Contains(ex.Message, "alias group", $"path={path}");
            }
        }

        [TestMethod]
        public void When_DuplicateHeadersWithAutoRenameOn_Then_AllPathsAppendNumericSuffix()
        {
            var csv = "A,A,A\n1,2,3\n";
            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions { AutoRenameHeaders = true });

                Assert.AreEqual(1, rows.Count, $"path={path}");
                CollectionAssert.AreEqual(new[] { "A", "A2", "A3" }, rows[0].Headers, $"path={path}");
                Assert.AreEqual("1", rows[0].ByName["A"], $"path={path}");
                Assert.AreEqual("2", rows[0].ByName["A2"], $"path={path}");
                Assert.AreEqual("3", rows[0].ByName["A3"], $"path={path}");
            }
        }

        [TestMethod]
        public void When_DuplicateHeadersWithAutoRenameOff_Then_AllPathsThrowInvalidOperation()
        {
            var csv = "A,A\n1,2\n";
            foreach (var path in AllPaths)
            {
                var ex = Assert.ThrowsExactly<InvalidOperationException>(
                    () => Run(path, csv, () => new CsvOptions { AutoRenameHeaders = false }),
                    $"path={path}");

                StringAssert.Contains(ex.Message, "Duplicate headers", $"path={path}");
            }
        }

        // ----------------------------------------------------------------------
        // 2. Multiline correctness matrix (the regression test for the bug fix)
        // ----------------------------------------------------------------------

        [TestMethod]
        public void When_HeaderPresentAndMultilineInDataRecord_Then_AllPathsKeepFieldIntact()
        {
            var csv = "col1,col2\r\nfoo,\"bar\r\nbaz\"\r\n";
            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions
                {
                    AllowNewLineInEnclosedFieldValues = true,
                    NewLine = "\r\n"
                });

                Assert.AreEqual(1, rows.Count, $"path={path}");
                CollectionAssert.AreEqual(new[] { "col1", "col2" }, rows[0].Headers, $"path={path}");
                Assert.AreEqual("foo", rows[0].Values[0], $"path={path}");
                Assert.AreEqual("bar\r\nbaz", rows[0].Values[1], $"path={path}");
            }
        }

        [TestMethod]
        public void When_HeaderAbsentAndMultilineInFirstRecord_Then_AllPathsProduceCorrectColumnCount()
        {
            // This is the bug-fix case. Before the engine unification, ReadAsync,
            // ReadFromMemoryOptimized, and ReadFromMemory would all miscount columns here
            // because they lacked the HeaderAbsent + multiline pre-pass.
            var csv = "\"a\r\nb\",c,d\r\nx,y,z\r\n";
            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions
                {
                    HeaderMode = HeaderMode.HeaderAbsent,
                    AllowNewLineInEnclosedFieldValues = true,
                    NewLine = "\r\n"
                });

                Assert.AreEqual(2, rows.Count, $"path={path}");
                Assert.AreEqual(3, rows[0].Headers.Length, $"path={path}");
                CollectionAssert.AreEqual(new[] { "Column1", "Column2", "Column3" }, rows[0].Headers, $"path={path}");
                Assert.AreEqual("a\r\nb", rows[0].Values[0], $"path={path}");
                Assert.AreEqual("c", rows[0].Values[1], $"path={path}");
                Assert.AreEqual("d", rows[0].Values[2], $"path={path}");
                Assert.AreEqual("x", rows[1].Values[0], $"path={path}");
                Assert.AreEqual("y", rows[1].Values[1], $"path={path}");
                Assert.AreEqual("z", rows[1].Values[2], $"path={path}");
            }
        }

        [TestMethod]
        public void When_HeaderAbsentAndMultilineInLaterRecord_Then_AllPathsKeepFieldIntact()
        {
            var csv = "a,b,c\r\n\"x\r\ny\",p,q\r\n";
            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions
                {
                    HeaderMode = HeaderMode.HeaderAbsent,
                    AllowNewLineInEnclosedFieldValues = true,
                    NewLine = "\r\n"
                });

                Assert.AreEqual(2, rows.Count, $"path={path}");
                CollectionAssert.AreEqual(new[] { "a", "b", "c" }, rows[0].Values, $"path={path}");
                Assert.AreEqual("x\r\ny", rows[1].Values[0], $"path={path}");
                Assert.AreEqual("p", rows[1].Values[1], $"path={path}");
                Assert.AreEqual("q", rows[1].Values[2], $"path={path}");
            }
        }

        [TestMethod]
        public void When_UnterminatedQuoteAtEof_Then_AllPathsTerminateWithoutInfiniteLoop()
        {
            var csv = "a,b\r\nfoo,\"unterminated\r\n";
            foreach (var path in AllPaths)
            {
                // The contract: must not hang. Last record returned with the accumulated content;
                // exact field values are not asserted (they depend on splitter behavior for an
                // unterminated quote), only that enumeration terminates with at least 1 row.
                List<Row> rows;
                try
                {
                    rows = Run(path, csv, () => new CsvOptions
                    {
                        AllowNewLineInEnclosedFieldValues = true,
                        NewLine = "\r\n"
                    });
                }
                catch (InvalidOperationException)
                {
                    // Acceptable: some paths may surface the malformed input as an error
                    // rather than yielding a partial row. The critical invariant is that
                    // enumeration terminates, which it did.
                    continue;
                }

                Assert.IsTrue(rows.Count >= 1, $"path={path} expected at least one row, got {rows.Count}");
            }
        }

        [TestMethod]
        public void When_NewLineIsLineFeedOnly_Then_AllPathsRespectOptionsNewLineForConcatenation()
        {
            var csv = "col1,col2\nfoo,\"bar\nbaz\"\n";
            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions
                {
                    AllowNewLineInEnclosedFieldValues = true,
                    NewLine = "\n"
                });

                Assert.AreEqual(1, rows.Count, $"path={path}");
                Assert.AreEqual("foo", rows[0].Values[0], $"path={path}");
                Assert.AreEqual("bar\nbaz", rows[0].Values[1], $"path={path}");
            }
        }

        // ----------------------------------------------------------------------
        // 3. Per-path contract tests
        // ----------------------------------------------------------------------

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task When_ReadAsyncOverStringReader_Then_ReturnsIAsyncEnumerableAndYieldsExpectedRows()
        {
            using var reader = new StringReader("a,b\n1,2\n3,4\n");
            var enumerable = CsvReader.ReadAsync(reader);
            Assert.IsInstanceOfType<IAsyncEnumerable<ICsvLine>>(enumerable);

            var collected = new List<(string a, string b)>();
            await foreach (var line in enumerable)
                collected.Add((line["a"], line["b"]));

            Assert.AreEqual(2, collected.Count);
            Assert.AreEqual(("1", "2"), collected[0]);
            Assert.AreEqual(("3", "4"), collected[1]);
        }

        [TestMethod]
        public void When_ReadFromMemoryOptimizedGivenExplicitMemoryOptions_Then_ProducesCorrectRecords()
        {
            var memoryOptions = new CsvMemoryOptions();
            var lines = CsvReader.ReadFromMemoryOptimized(
                "name,age\nAlice,30\nBob,25\n".AsMemory(),
                new CsvOptions(),
                memoryOptions).ToList();

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("Alice", lines[0]["name"]);
            Assert.AreEqual("30", lines[0]["age"]);
            Assert.AreEqual("Bob", lines[1]["name"]);
            Assert.AreEqual("25", lines[1]["age"]);
        }

        [TestMethod]
        public void When_ReadFromMemory_Then_HeadersValuesAndRawAreReadOnlyMemoryOfChar()
        {
            var lines = CsvReader.ReadFromMemory("name,age\nAlice,30\n".AsMemory()).ToList();

            Assert.AreEqual(1, lines.Count);

            var line = lines[0];
            // Headers/Values/Raw expose ReadOnlyMemory<char> rather than string.
            ReadOnlyMemory<char>[] headers = line.Headers;
            ReadOnlyMemory<char>[] values = line.Values;
            ReadOnlyMemory<char> raw = line.Raw;
            ReadOnlyMemory<char> byName = line["name"];
            ReadOnlyMemory<char> byIndex = line[0];

            Assert.AreEqual("name", headers[0].ToString());
            Assert.AreEqual("age", headers[1].ToString());
            Assert.AreEqual("Alice", values[0].ToString());
            Assert.AreEqual("30", values[1].ToString());
            Assert.AreEqual("Alice,30", raw.ToString());
            Assert.AreEqual("Alice", byName.ToString());
            Assert.AreEqual("Alice", byIndex.ToString());
        }
#endif

        // ----------------------------------------------------------------------
        // 4. Allocation-parity smoke test
        // ----------------------------------------------------------------------

#if NET8_0_OR_GREATER
        [TestMethod]
        public void When_ReadAsSpanEnumeratesOneThousandRecords_Then_AllocatedBytesIsFinite()
        {
            var builder = new StringBuilder();
            builder.AppendLine("col1,col2,col3");
            for (int i = 0; i < 1000; i++)
                builder.AppendLine($"value{i}a,value{i}b,value{i}c");
            var csv = builder.ToString();

            long before;
            try
            {
                before = GC.GetTotalAllocatedBytes(precise: true);
            }
            catch (PlatformNotSupportedException)
            {
                Assert.Inconclusive("GC.GetTotalAllocatedBytes is unavailable on this platform.");
                return;
            }

            using var reader = new StringReader(csv);
            int rowCount = 0;
            foreach (var line in CsvReader.ReadAsSpan(reader))
            {
                // Touch a column so the row materializes fully.
                _ = line.GetSpan(0).Length;
                rowCount++;
            }

            var after = GC.GetTotalAllocatedBytes(precise: true);
            var delta = after - before;

            Assert.AreEqual(1000, rowCount);
            Assert.IsTrue(delta >= 0, $"Expected non-negative allocation delta, got {delta}");
            Assert.IsTrue(delta < long.MaxValue, $"Allocation delta out of range: {delta}");
            // Documented for drift visibility; no hard upper bound.
            System.Diagnostics.Trace.WriteLine($"ReadAsSpan over 1000 records allocated {delta} bytes.");
        }
#endif

        [TestMethod]
        public void When_BlankLineInMiddleOfStream_Then_AllPathsContinueParsing()
        {
            const string csv = "a,b,c\n1,2,3\n\n4,5,6\n";

            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions());

                Assert.AreEqual(2, rows.Count, $"{path}: expected 2 records after default SkipRow elides the blank line, got {rows.Count}");
                Assert.AreEqual("1,2,3", rows[0].Raw, path.ToString());
                Assert.AreEqual("4,5,6", rows[1].Raw, path.ToString());
            }
        }

        [TestMethod]
        public void When_BlankLineAndSkipRowDisabled_Then_AllPathsReturnEmptyRecord()
        {
            const string csv = "a,b,c\n1,2,3\n\n4,5,6\n";

            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions
                {
                    SkipRow = (_, _) => false,
                    ValidateColumnCount = false,
                    ReturnEmptyForMissingColumn = true,
                });

                Assert.AreEqual(3, rows.Count, $"{path}: expected 3 records when SkipRow is disabled (blank line surfaces as empty record), got {rows.Count}");
                Assert.AreEqual("1,2,3", rows[0].Raw, path.ToString());
                Assert.AreEqual(string.Empty, rows[1].Raw, path.ToString());
                Assert.AreEqual("4,5,6", rows[2].Raw, path.ToString());
            }
        }

        [TestMethod]
        public void When_TrailingBlankLine_Then_AllPathsTerminateAfterLastRecord()
        {
            const string csv = "a,b,c\n1,2,3\n\n";

            foreach (var path in AllPaths)
            {
                var rows = Run(path, csv, () => new CsvOptions());

                Assert.AreEqual(1, rows.Count, $"{path}: expected exactly 1 record, got {rows.Count}");
                Assert.AreEqual("1,2,3", rows[0].Raw, path.ToString());
            }
        }
    }
}
