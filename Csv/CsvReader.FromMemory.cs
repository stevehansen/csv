#if NETCOREAPP3_1 || NETSTANDARD2_1

using System;
using System.Collections.Generic;
using System.Linq;

using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;

namespace Csv
{
    partial class CsvReader
    {
        /// <summary>
        /// Reads the lines from the csv string.
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
            while (!(line = csv.ReadLine(ref position)).IsEmpty)
            {
                index++;
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(line.Span, options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(line, options) : CreateDefaultHeaders(line.Span, options);

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
                if (options.AllowNewLineInEnclosedFieldValues)
                {
                    while (record.RawSplitLine.Any(f => IsUnterminatedQuotedValue(f.AsSpan(), options)))
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

        private sealed class ReadLineFromMemory : ICsvLineFromMemory
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
            private MemoryText[]? rawSplitLine;
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

            internal MemoryText[] RawSplitLine
            {
                get
                {
                    if (rawSplitLine == null)
                    {
                        lock (headerLookup)
                        {
                            rawSplitLine ??= SplitLine(Raw, options);
                        }
                    }
                    return rawSplitLine;
                }
            }

            public MemoryText[] Values => Line;

            private MemoryText[] Line
            {
                get
                {
                    if (parsedLine == null)
                    {
                        var raw = RawSplitLine;

                        if (options.ValidateColumnCount && raw.Length != Headers.Length)
                            throw new InvalidOperationException($"Expected {Headers.Length}, got {raw.Length} columns.");

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