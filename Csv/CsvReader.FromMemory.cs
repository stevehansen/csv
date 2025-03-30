#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;

namespace Csv
{
    partial class CsvReader
    {
        /// <summary>
        /// Reads the lines from the csv string with optimized Span-based parsing.
        /// </summary>
        /// <param name="csv">The csv string to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLineFromMemory> ReadFromMemory(MemoryText csv, CsvOptions? options = null)
        {
            // NOTE: Logic is copied in ReadImpl/ReadImplAsync/ReadFromMemory
            options ??= new CsvOptions();

            MemoryText line;
            var index = 0;
            var position = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;

            // Optimization: Try to quickly determine separator by scanning the first line
            if (options.Separator == '\0' && csv.Length > 0)
            {
                var firstLineEnd = csv.Span.IndexOfAny('\r', '\n');
                if (firstLineEnd < 0) firstLineEnd = csv.Length;
                TryGetQuickSeparator(csv.Span[..firstLineEnd], out var quickSeparator);
                options.Separator = quickSeparator;
            }

            while (!(line = csv.ReadLine(ref position)).IsEmpty)
            {
                index++;
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(line.Span, options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    // Optimization: Use span-based header parsing when possible
                    headers = skipInitialLine ? GetSpanHeaders(line, options) : CreateDefaultHeaders(line, options);

                    try
                    {
                        headerLookup = headers
                            .Select((h, idx) => (h, idx))
                            .ToDictionary(h => h.Item1.AsString(), h => h.Item2, options.Comparer);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException("Duplicate headers detected in HeaderPresent mode. If you don't have a header you can set the HeaderMode to HeaderAbsent.");
                    }

                    var aliases = options.Aliases;
                    if (aliases != null)
                    {
                        // NOTE: For each group we need at most 1 match (i.e. SingleOrDefault)
                        foreach (var aliasGroup in aliases)
                        {
                            var groupIndex = -1;
                            foreach (var alias in aliasGroup)
                            {
                                if (headerLookup.TryGetValue(alias, out var aliasIndex))
                                {
                                    if (groupIndex != -1)
                                        throw new InvalidOperationException("Found multiple matches within alias group: " + string.Join(";", aliasGroup));

                                    groupIndex = aliasIndex;
                                }
                            }

                            if (groupIndex != -1)
                            {
                                foreach (var alias in aliasGroup)
                                    headerLookup[alias] = groupIndex;
                            }
                        }
                    }

                    if (skipInitialLine)
                        continue;
                }

                var record = new ReadLineFromMemory(headers, headerLookup, index, line, options);

                // Optimize multiline field handling with span-based operations
                if (options.AllowNewLineInEnclosedFieldValues)
                {
                    while (HasUnterminatedQuotedFields(record.RawSplitLine, options))
                    {
                        var nextLine = csv.ReadLine(ref position);
                        if (nextLine.IsEmpty)
                            break;

                        line = StringHelpers.Concat(line, options.NewLine, nextLine);
                        record = new ReadLineFromMemory(headers, headerLookup, index, line, options);
                    }
                }

                yield return record;
            }
        }

        /// <summary>
        /// Fast check for unterminated quoted fields in a collection
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasUnterminatedQuotedFields(IList<MemoryText> fields, CsvOptions options)
        {
            // Optimization: Only check the last field first since that's most common
            if (fields.Count > 0 && CsvLineSplitter.IsUnterminatedQuotedValue(fields[^1].Span, options))
                return true;

            // If we have a multiline quote in other fields, check all fields
            for (var i = 0; i < fields.Count - 1; i++)
            {
                if (CsvLineSplitter.IsUnterminatedQuotedValue(fields[i].Span, options))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Try to determine the best separator by analyzing frequency
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetQuickSeparator(SpanText headerLine, out char separator)
        {
            // Common separators
            SpanText candidates = stackalloc char[] { ',', ';', '\t', '|' };

            // Count occurrences for each candidate
            Span<int> counts = stackalloc int[candidates.Length];

            for (var i = 0; i < headerLine.Length; i++)
            {
                for (var j = 0; j < candidates.Length; j++)
                {
                    if (headerLine[i] == candidates[j])
                    {
                        counts[j]++;
                    }
                }
            }

            // Find the most common separator
            var maxCount = 0;
            var maxIndex = -1;

            for (var i = 0; i < counts.Length; i++)
            {
                if (counts[i] > maxCount)
                {
                    maxCount = counts[i];
                    maxIndex = i;
                }
            }

            if (maxCount > 0)
            {
                separator = candidates[maxIndex];
                return true;
            }

            separator = ','; // Default
            return false;
        }

        /// <summary>
        /// Optimized span-based header parsing
        /// </summary>
        private static MemoryText[] GetSpanHeaders(MemoryText line, CsvOptions options)
        {
            // Get an instance of the line splitter
            var splitter = options.Splitter;

            // Split and trim the headers
            var values = splitter.Split(line, options);
            return Trim(values, options);
        }

        private sealed class ReadLineFromMemory : ICsvLineFromMemory
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
            private IList<MemoryText>? rawSplitLine;
            private MemoryText[]? parsedLine;

            public ReadLineFromMemory(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, MemoryText raw, CsvOptions options)
            {
                this.headerLookup = headerLookup;
                this.options = options;
                Headers = headers;
                Raw = raw;
                Index = index;
            }

            public MemoryText[] Headers { get; }

            public MemoryText Raw { get; }

            public int Index { get; }

            public int ColumnCount => Line.Length;

            public bool HasColumn(string name) => headerLookup.ContainsKey(name);

            internal IList<MemoryText> RawSplitLine => rawSplitLine ??= SplitLine(Raw, options);

            public MemoryText[] Values => Line;

            private MemoryText[] Line
            {
                get
                {
                    if (parsedLine == null)
                    {
                        var raw = RawSplitLine;

                        if (options.ValidateColumnCount && raw.Count != Headers.Length)
                            throw new InvalidOperationException($"Expected {Headers.Length}, got {raw.Count} columns.");

                        parsedLine = Trim(raw, options);
                    }

                    return parsedLine;
                }
            }

            MemoryText ICsvLineFromMemory.this[string name]
            {
                get
                {
                    if (!headerLookup.TryGetValue(name, out var index))
                    {
                        if (options.ReturnEmptyForMissingColumn)
                            return MemoryText.Empty;

                        throw new ArgumentOutOfRangeException(nameof(name), name, $"Header '{name}' does not exist. Expected one of {string.Join("; ", Headers)}");
                    }

                    try
                    {
                        return Line[index];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw new InvalidOperationException($"Invalid row, missing {name} header, expected {Headers.Length} columns, got {Line.Length} columns.");
                    }
                }
            }

            MemoryText ICsvLineFromMemory.this[int index] => Line[index];

            public override string ToString()
            {
                return Raw.AsString();
            }
        }
    }
}

#endif