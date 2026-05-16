#if NET8_0_OR_GREATER

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
            => Enumerate<MemoryReaderLineSource, MemoryRowFactory, ReadLineFromMemory>(new MemoryReaderLineSource(csv), default, options ?? new CsvOptions());

        internal sealed class ReadLineFromMemory : ICsvLineFromMemory
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

            public bool LineHasColumn(string name)
            {
                if (!headerLookup.TryGetValue(name, out var index))
                    return false;

                return RawSplitLine.Count > index;
            }

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