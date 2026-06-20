using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csv.Tests
{
    /// <summary>
    /// Boundary tests for the three hardening fixes that came out of unifying the four
    /// row classes into <c>CsvReader.CsvLine&lt;TPolicy&gt;</c> (issue #132):
    ///   FIX 1 - a missing-column lookup under <see cref="CsvOptions.ReturnEmptyForMissingColumn"/>
    ///           returns a non-null empty value on every read path / interface surface.
    ///   FIX 2 - <c>GetBlock</c> sub-lines keep their pre-extracted cell values instead of
    ///           re-splitting the synthesized full <c>Raw</c>.
    ///   FIX 3 - <c>Raw</c> / <c>RawMemory</c> round-trip the original line text, and on the
    ///           memory paths <c>RawMemory</c> aliases the source buffer (zero-copy intent).
    /// These run net9.0 only (the test project's TFM), so the netstandard2.0-specific notes
    /// below document the contract the shared code must keep on that target.
    /// </summary>
    [TestClass]
    public class RowUnificationTests
    {
        // ------------------------------------------------------------------
        // FIX 1: empty-value policy for a missing column, on every read path.
        // ------------------------------------------------------------------
        //
        // The shared accessor returns CsvLine<TPolicy>.Empty ("".AsMemory()) for a missing
        // column when ReturnEmptyForMissingColumn is set. The risk this pins is
        // netstandard2.0-specific: there MemoryText == string, so a `default(MemoryText)`
        // empty would be NULL, not "". Returning an explicit "".AsMemory() keeps the value
        // non-null on every target. These tests run on net9 only, so they assert the
        // observable net9 contract (Length == 0 on the memory surfaces, non-null "" on the
        // ICsvLine string surface) and document the ns2.0 intent.

        private const string TwoCol = "a,b\n1,2\n";

        private static CsvOptions ReturnEmptyOptions() => new CsvOptions { ReturnEmptyForMissingColumn = true };

        [TestMethod]
        public void When_MissingColumnAndReturnEmpty_Then_ReadFromTextStringIndexerReturnsNonNullEmpty()
        {
            var line = CsvReader.ReadFromText(TwoCol, ReturnEmptyOptions()).Single();

            string value = line["NoSuchCol"];

            // ICsvLine.this[string] is a reference type, so guard non-null explicitly:
            // on ns2.0 a `default` empty MemoryText would surface here as null.
            Assert.IsNotNull(value, "missing-column lookup must not be null");
            Assert.AreEqual(string.Empty, value);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public void When_MissingColumnAndReturnEmpty_Then_ReadFromTextAsSpanSurfacesAreEmpty()
        {
            var line = CsvReader.ReadFromTextAsSpan(TwoCol, ReturnEmptyOptions()).Single();

            string value = line["NoSuchCol"];
            Assert.IsNotNull(value, "missing-column lookup must not be null");
            Assert.AreEqual(string.Empty, value);

            Assert.AreEqual(0, line.GetMemory("NoSuchCol").Length);
            Assert.AreEqual(0, line.GetSpan("NoSuchCol").Length);
        }

        [TestMethod]
        public void When_MissingColumnAndReturnEmpty_Then_ReadFromMemoryStringIndexerIsEmpty()
        {
            var line = CsvReader.ReadFromMemory("a,b\n1,2".AsMemory(), ReturnEmptyOptions()).Single();

            // ICsvLineFromMemory.this[string] returns ReadOnlyMemory<char>; the contract is
            // an empty (Length == 0), non-default slice rather than a null backing string.
            Assert.AreEqual(0, line["NoSuchCol"].Length);
        }

        [TestMethod]
        public void When_MissingColumnAndReturnEmpty_Then_ReadFromMemoryOptimizedSurfacesAreEmpty()
        {
            var line = CsvReader.ReadFromMemoryOptimized(TwoCol.AsMemory(), ReturnEmptyOptions()).Single();

            string value = line["NoSuchCol"];
            Assert.IsNotNull(value, "missing-column lookup must not be null");
            Assert.AreEqual(string.Empty, value);

            Assert.AreEqual(0, line.GetMemory("NoSuchCol").Length);
            Assert.AreEqual(0, line.GetSpan("NoSuchCol").Length);
        }
#endif

        // ------------------------------------------------------------------
        // FIX 2: GetBlock pre-seeded values survive (no re-split of synthesized Raw).
        // ------------------------------------------------------------------
        //
        // GetBlock builds each sub-line by pre-extracting the selected cells and passing them
        // as parsedValues, while it passes the ORIGINAL full line text as the sub-line's Raw.
        // If parsedValues pre-seeding were dropped, the sub-line would lazily re-split its Raw
        // (the whole original row) and surface the WRONG columns. The cases below select column
        // subsets where a re-split of Raw would visibly disagree with the pre-seeded values, so
        // the test fails loudly if the pre-seeding regresses.

        [TestMethod]
        public void When_GetBlockSelectsTrailingColumnSubset_Then_SubLineKeepsPreExtractedValuesNotResplitRaw()
        {
            // 4 columns; select only the last two (col_start = 2). A re-split of the sub-line's
            // Raw ("w,x,y,z") would yield ["w","x"] for the first two slots, whereas the correct
            // pre-extracted values for headers [c,d] are ["y","z"].
            var lines = CsvReader.ReadFromText("a,b,c,d\nw,x,y,z\n").ToList();

            var block = lines.GetBlock(row_start: 0, row_length: -1, col_start: 2, col_length: -1).ToList();

            Assert.AreEqual(1, block.Count);
            var sub = block[0];

            CollectionAssert.AreEqual(new[] { "c", "d" }, sub.Headers);
            CollectionAssert.AreEqual(new[] { "y", "z" }, sub.Values,
                "sub-line must keep the pre-extracted cell values, not a re-split of the full Raw");
            Assert.AreEqual("y", sub["c"]);
            Assert.AreEqual("z", sub["d"]);
            Assert.AreEqual(2, sub.ColumnCount);

            // The sub-line's Raw is the original full row; re-splitting it would give 4 fields,
            // so this confirms the values came from pre-seeding, not from splitting Raw.
            Assert.AreEqual(4, sub.Raw.Split(',').Length);
        }

        [TestMethod]
        public void When_GetBlockSelectsMiddleColumnSubset_Then_ValuesMatchSelectedCells()
        {
            // Select exactly one middle column (col_start = 1, col_length = 1). A re-split of Raw
            // would put "w" in slot 0; the correct pre-extracted value for header [b] is "x".
            var lines = CsvReader.ReadFromText("a,b,c,d\nw,x,y,z\n").ToList();

            var block = lines.GetBlock(row_start: 0, row_length: -1, col_start: 1, col_length: 1).ToList();

            Assert.AreEqual(1, block.Count);
            var sub = block[0];

            CollectionAssert.AreEqual(new[] { "b" }, sub.Headers);
            CollectionAssert.AreEqual(new[] { "x" }, sub.Values);
            Assert.AreEqual("x", sub["b"]);
            Assert.AreEqual(1, sub.ColumnCount);
        }

        // ------------------------------------------------------------------
        // FIX 3: Raw identity / zero-copy intent.
        // ------------------------------------------------------------------

        [TestMethod]
        public void When_ReadFromText_Then_RawIsTheOriginalLineText()
        {
            var lines = CsvReader.ReadFromText("a,b\n1,2\n3,4\n").ToList();

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("1,2", lines[0].Raw);
            Assert.AreEqual("3,4", lines[1].Raw);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public void When_ReadFromTextAsSpan_Then_RawAndRawMemoryRoundTripTheLineText()
        {
            var lines = CsvReader.ReadFromTextAsSpan("a,b\n1,2\n3,4\n").ToList();

            Assert.AreEqual(2, lines.Count);

            // Raw (string) and RawMemory (ReadOnlyMemory<char>) describe the same line text.
            Assert.AreEqual("1,2", lines[0].Raw);
            Assert.AreEqual("1,2", lines[0].RawMemory.ToString());
            Assert.AreEqual("3,4", lines[1].Raw);
            Assert.AreEqual("3,4", lines[1].RawMemory.ToString());
        }

        [TestMethod]
        public void When_ReadFromMemoryOptimized_Then_RawMemoryAliasesTheSourceBufferZeroCopy()
        {
            // The memory paths slice the source memory rather than copying, so a row's RawMemory
            // must point INTO the original backing string. This expresses the zero-alloc intent
            // of FIX 3 the testable way (reference identity), avoiding a brittle byte-count assert.
            var source = "a,b\n1,2\n3,4\n";
            var lines = CsvReader.ReadFromMemoryOptimized(source.AsMemory()).ToList();

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("1,2", lines[0].RawMemory.ToString());
            Assert.AreEqual("3,4", lines[1].RawMemory.ToString());

            // MemoryMarshal.TryGetString recovers the backing string of a string-derived slice.
            // Identical (reference-equal) backing string proves RawMemory is a view, not a copy.
            Assert.IsTrue(MemoryMarshal.TryGetString(lines[0].RawMemory, out var backing, out _, out _),
                "RawMemory should be backed by a string on the memory path");
            Assert.AreSame(source, backing,
                "RawMemory must alias the source buffer rather than allocate a copy");
        }

        [TestMethod]
        public void When_ReadFromMemory_Then_RawIsTheOriginalLineTextAndAliasesSource()
        {
            var source = "a,b\n1,2\n3,4\n";
            var lines = CsvReader.ReadFromMemory(source.AsMemory()).ToList();

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("1,2", lines[0].Raw.ToString());
            Assert.AreEqual("3,4", lines[1].Raw.ToString());

            Assert.IsTrue(MemoryMarshal.TryGetString(lines[0].Raw, out var backing, out _, out _),
                "Raw should be backed by a string on the memory path");
            Assert.AreSame(source, backing,
                "Raw must alias the source buffer rather than allocate a copy");
        }
#endif
    }
}
