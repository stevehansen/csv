using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if NET8_0_OR_GREATER
using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;
#else
using MemoryText = System.String;
using SpanText = System.String;
#endif

namespace Csv
{
    /// <summary>
    /// Helper class to read csv (comma separated values) data.
    /// </summary>
    public static partial class CsvReader
    {
        /// <summary>
        /// Reads the records from the reader.
        /// </summary>
        /// <param name="reader">The text reader to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLine> Read(TextReader reader, CsvOptions? options = null)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            return ReadImpl(reader, options);
        }

        /// <summary>
        /// Reads the records from the stream.
        /// </summary>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLine> ReadFromStream(Stream stream, CsvOptions? options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return ReadFromStreamImpl(stream, options);
        }

        /// <summary>
        /// Reads the records from the csv string.
        /// </summary>
        /// <param name="csv">The csv string to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLine> ReadFromText(string csv, CsvOptions? options = null)
        {
            if (csv == null)
                throw new ArgumentNullException(nameof(csv));

            return ReadFromTextImpl(csv, options);
        }

#if NET8_0_OR_GREATER

        /// <summary>
        /// Reads the records from the reader with enhanced Span/Memory support.
        /// </summary>
        /// <param name="reader">The text reader to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLineSpan> ReadAsSpan(TextReader reader, CsvOptions? options = null)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            return ReadSpanImpl(reader, options);
        }

        /// <summary>
        /// Reads the records from the stream with enhanced Span/Memory support.
        /// </summary>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLineSpan> ReadFromStreamAsSpan(Stream stream, CsvOptions? options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return ReadFromStreamSpanImpl(stream, options);
        }

        /// <summary>
        /// Reads the records from the csv string with enhanced Span/Memory support.
        /// </summary>
        /// <param name="csv">The csv string to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLineSpan> ReadFromTextAsSpan(string csv, CsvOptions? options = null)
        {
            if (csv == null)
                throw new ArgumentNullException(nameof(csv));

            return ReadFromTextSpanImpl(csv, options);
        }

        private static IEnumerable<ICsvLineSpan> ReadFromStreamSpanImpl(Stream stream, CsvOptions? options)
        {
            using (var reader = new StreamReader(stream))
            {
                foreach (var line in ReadSpanImpl(reader, options))
                    yield return line;
            }
        }

        private static IEnumerable<ICsvLineSpan> ReadFromTextSpanImpl(string csv, CsvOptions? options)
        {
            using (var reader = new StringReader(csv))
            {
                foreach (var line in ReadSpanImpl(reader, options))
                    yield return line;
            }
        }

        private static IEnumerable<ICsvLineSpan> ReadSpanImpl(TextReader reader, CsvOptions? options)
            => Enumerate<TextReaderLineSource, SpanRowFactory, CsvLine<DefaultTrimSplit>>(new TextReaderLineSource(reader), default, options ?? new CsvOptions());

        /// <summary>
        /// Reads CSV data from memory with enhanced memory management options.
        /// </summary>
        /// <param name="csv">The CSV data as ReadOnlyMemory.</param>
        /// <param name="options">The CSV parsing options.</param>
        /// <param name="memoryOptions">The memory management options.</param>
        /// <returns>An enumerable of CSV records with memory optimization.</returns>
        public static IEnumerable<ICsvLineSpan> ReadFromMemoryOptimized(ReadOnlyMemory<char> csv, CsvOptions? options = null, CsvMemoryOptions? memoryOptions = null)
        {
            options ??= new CsvOptions();
            memoryOptions ??= new CsvMemoryOptions();
            memoryOptions.Validate();

            return ReadFromMemoryOptimizedImpl(csv, options, memoryOptions);
        }

        /// <summary>
        /// Creates a buffer writer for optimized CSV writing.
        /// </summary>
        /// <param name="headers">The CSV headers.</param>
        /// <param name="separator">The column separator.</param>
        /// <param name="memoryOptions">The memory options.</param>
        /// <returns>A buffer writer instance.</returns>
        public static CsvBufferWriter CreateBufferWriter(ReadOnlySpan<string> headers, char separator = ',', CsvMemoryOptions? memoryOptions = null)
        {
            memoryOptions ??= new CsvMemoryOptions();
            var writer = new CsvBufferWriter(memoryOptions);
            
            var headerMemories = new ReadOnlyMemory<char>[headers.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                headerMemories[i] = headers[i].AsMemory();
            }

            writer.WriteRow(headerMemories, separator);
            return writer;
        }

        private static IEnumerable<ICsvLineSpan> ReadFromMemoryOptimizedImpl(ReadOnlyMemory<char> csv, CsvOptions options, CsvMemoryOptions memoryOptions)
            => Enumerate<MemorySliceLineSource, OptimizedRowFactory, CsvLine<OptimizedTrimSplit>>(new MemorySliceLineSource(csv), new OptimizedRowFactory(memoryOptions), options);

#endif

        private static IEnumerable<ICsvLine> ReadFromStreamImpl(Stream stream, CsvOptions? options)
        {
            using (var reader = new StreamReader(stream))
            {
                foreach (var line in ReadImpl(reader, options))
                    yield return line;
            }
        }

        private static IEnumerable<ICsvLine> ReadFromTextImpl(string csv, CsvOptions? options)
        {
            using (var reader = new StringReader(csv))
            {
                foreach (var line in ReadImpl(reader, options))
                    yield return line;
            }
        }

        private static IEnumerable<ICsvLine> ReadImpl(TextReader reader, CsvOptions? options)
            => Enumerate<TextReaderLineSource, StringRowFactory, CsvLine<DefaultTrimSplit>>(new TextReaderLineSource(reader), default, options ?? new CsvOptions());

#if NET8_0_OR_GREATER
        /// <summary>
        /// Reads the records from the reader.
        /// </summary>
        /// <param name="reader">The text reader to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IAsyncEnumerable<ICsvLine> ReadAsync(TextReader reader, CsvOptions? options = null)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            return ReadImplAsync(reader, options);
        }

        /// <summary>
        /// Reads the records from the stream.
        /// </summary>
        /// <param name="stream">The stream to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IAsyncEnumerable<ICsvLine> ReadFromStreamAsync(Stream stream, CsvOptions? options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            static async IAsyncEnumerable<ICsvLine> Impl(Stream stream, CsvOptions? options)
            {
                using var reader = new StreamReader(stream);
                await foreach (var line in ReadImplAsync(reader, options))
                    yield return line;
            }

            return Impl(stream, options);
        }

        /// <summary>
        /// Reads the records from the csv string.
        /// </summary>
        /// <param name="csv">The csv string to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IAsyncEnumerable<ICsvLine> ReadFromTextAsync(string csv, CsvOptions? options = null)
        {
            if (csv == null)
                throw new ArgumentNullException(nameof(csv));

            static async IAsyncEnumerable<ICsvLine> Impl(string csv, CsvOptions? options)
            {
                using var reader = new StringReader(csv);
                await foreach (var line in ReadImplAsync(reader, options))
                    yield return line;
            }

            return Impl(csv, options);
        }

        private static IAsyncEnumerable<ICsvLine> ReadImplAsync(TextReader reader, CsvOptions? options)
            => EnumerateAsync<AsyncTextReaderLineSource, StringRowFactory, CsvLine<DefaultTrimSplit>>(new AsyncTextReaderLineSource(reader), default, options ?? new CsvOptions());
#endif

        private static char AutoDetectSeparator(SpanText sampleLine)
        {
            // NOTE: Try simple 'detection' of possible separator
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var ch in sampleLine)
            {
                if (ch == ';' || ch == '\t')
                    return ch;
            }

            return ',';
        }

        private static MemoryText[] CreateDefaultHeaders(MemoryText line, CsvOptions options)
        {
            var columnCount = options.Splitter.Split(line, options).Count;

            var headers = new MemoryText[columnCount];
            for (var i = 0; i < headers.Length; i++)
                headers[i] = $"Column{i + 1}".AsMemory();

            return headers;
        }

        private static MemoryText[] GetHeaders(MemoryText line, CsvOptions options)
        {
            return Trim(SplitLine(line, options), options);
        }

        private static Dictionary<string, int> CreateHeaderLookup(MemoryText[] headers, CsvOptions options)
        {
            if (!options.AutoRenameHeaders)
            {
                // Original behavior: throw on duplicates
#if NET8_0_OR_GREATER
                return headers
                    .Select((h, idx) => (h, idx))
                    .ToDictionary(h => h.Item1.AsString(), h => h.Item2, options.Comparer);
#else
                return headers
                    .Select((h, idx) => Tuple.Create(h, idx))
                    .ToDictionary(h => h.Item1.AsString(), h => h.Item2, options.Comparer);
#endif
            }

            // New behavior: auto-rename duplicates and empty headers
            var headerLookup = new Dictionary<string, int>(options.Comparer ?? StringComparer.Ordinal);
            var headerCounts = new Dictionary<string, int>(options.Comparer ?? StringComparer.Ordinal);

            for (var i = 0; i < headers.Length; i++)
            {
                var headerText = headers[i].AsString();

                // Replace empty headers with "Empty"
                if (string.IsNullOrWhiteSpace(headerText))
                {
                    headerText = "Empty";
                }

                // Check if we've seen this header before
                if (headerCounts.TryGetValue(headerText, out var count))
                {
                    // Increment the count and create a unique name
                    count++;
                    headerCounts[headerText] = count;
                    var uniqueName = $"{headerText}{count}";

                    // Update the header in the array
                    headers[i] = uniqueName.AsMemory();
                    headerLookup[uniqueName] = i;
                }
                else
                {
                    // First occurrence of this header
                    headerCounts[headerText] = 1;

                    // Update the header in the array if it was empty
                    if (headers[i].AsString() != headerText)
                    {
                        headers[i] = headerText.AsMemory();
                    }

                    headerLookup[headerText] = i;
                }
            }

            return headerLookup;
        }

        private static void InitializeOptions(SpanText line, CsvOptions options)
        {
            if (options.Separator == '\0')
                options.Separator = AutoDetectSeparator(line);

            options.Splitter = CsvLineSplitter.Get(options);
        }

        private static IList<MemoryText> SplitLine(MemoryText line, CsvOptions options, int? capacity = null)
        {
            return options.Splitter.Split(line, options, capacity);
        }

        private static MemoryText[] Trim(IList<MemoryText> line, CsvOptions options)
        {
            var trimmed = new MemoryText[line.Count]; // TODO: Mutate existing array?
            for (var i = 0; i < line.Count; i++)
            {
                var str = line[i];
                if (options.TrimData)
                    str = str.Trim();

                if (str.Length > 1 && options.AllowEnclosedFieldValues)
                {
#if NET8_0_OR_GREATER
                    var span = str.Span;
                    if (span[0] == '"' && span[^1] == '"')
                    {
                        str = str[1..^1].Unescape('"', '"');

                        if (options.AllowBackSlashToEscapeQuote)
                            str = str.Unescape('\\', '"');
                    }
                    else if (options.AllowSingleQuoteToEncloseFieldValues && span[0] == '\'' && span[^1] == '\'')
                        str = str[1..^1];
#else
                    if (str[0] == '"' && str[str.Length - 1] == '"')
                    {
                        str = str.Substring(1, str.Length - 2).Replace("\"\"", "\"");

                        if (options.AllowBackSlashToEscapeQuote)
                            str = str.Replace("\\\"", "\"");
                    }
                    else if (options.AllowSingleQuoteToEncloseFieldValues && str[0] == '\'' && str[str.Length - 1] == '\'')
                        str = str.Substring(1, str.Length - 2);
#endif
                }

                trimmed[i] = str;
            }

            return trimmed;
        }
        /// <summary>
        /// Gets a single column from the entire Enumeration of `ICsvLine`
        /// </summary>
        /// <param name="lines">The enumeration of `ICsvLine`</param>
        /// <param name="columnNo">The index (starting from 0) of the the column
        /// to extract</param>
        /// <param name="transform">The transformation function to parse
        /// from the string values</param>
        /// <typeparam name="T">The datatype to transform
        /// the string inputs into</typeparam>
        /// <returns>An enumeration of the transformed
        /// values of the selected column</returns>
        public static IEnumerable<T> GetColumn<T>(this IEnumerable<ICsvLine> lines, int columnNo, Func<string, T> transform) => lines.Select(x => transform(x[columnNo]));

        /// <summary>
        /// Gets a single column from the entire Enumeration of `ICsvLine`
        /// </summary>
        /// <param name="lines">The enumeration of `ICsvLine`</param>
        /// <param name="columnNo">The index (starting from 0) of the the
        /// column to extract</param>
        /// <returns>An enumerations of the string values of
        /// the selected column</returns>
        public static IEnumerable<string> GetColumn(this IEnumerable<ICsvLine> lines, int columnNo) => lines.GetColumn(columnNo, (x) => x);
        /// <summary>
        /// Gets a range/block of values from the given enumeration of `ICsvLine`
        /// </summary>
        /// <param name="lines">The enumeration of `ICsvLine`</param>
        /// <param name="row_start">The index(starting from 0) of the rows to start the capture of.
        /// Default value is -1</param>
        /// <param name="row_length">The number of rows to capture from the start row. If the default value
        ///  (or any negative number) is passed, selects all the rows till the end</param>
        /// <param name="col_start">The index(starting from 0) of all the columns to start the capture of.</param>
        /// <param name="col_length">The number of rows to capture from the start column.
        /// If the default value (or any negative number) is passed, selects all the rows till the end</param>
        /// <returns></returns>
        public static IEnumerable<ICsvLine> GetBlock(this IEnumerable<ICsvLine> lines, int row_start = 0, int row_length = -1, int col_start = 0, int col_length = -1)
        {
#if NET8_0_OR_GREATER
            if (row_length == 0 || col_length == 0) return Array.Empty<ICsvLine>();
#else
            if (row_length == 0 || col_length == 0) return new ICsvLine[0];
#endif
            if (row_length < 0)
            {
                return lines.Skip(row_start).Select(x => SubLine(x, col_start, col_length));
            }
            else
            {
                return lines.Skip(row_start).Take(row_length).Select(x => SubLine(x, col_start, col_length));
            }
            ICsvLine SubLine(ICsvLine line, int start, int length)
            {
                MemoryText[] headers;
                if (length < 0 || start + length >= line.ColumnCount)
                    headers = line.Headers.Skip(start).Select(x=>x.AsMemory()).ToArray();
                else
                    headers = line.Headers.Skip(start).Take(length).Select(x=>x.AsMemory()).ToArray();
                MemoryText[] values = headers.Select(x => line[x.ToString()].AsMemory()).ToArray();
                Dictionary<string, int> map = Enumerable.Range(0, headers.Length).ToDictionary(x => headers[x].ToString());
#if NET8_0_OR_GREATER
                return new CsvLine<DefaultTrimSplit>(headers, map, line.Index, line.Raw.AsMemory(), line.Raw, new CsvOptions()) { parsedValues = values };
#else
                return new CsvLine<DefaultTrimSplit>(headers, map, line.Index, line.Raw, line.Raw, new CsvOptions()) { parsedValues = values };
#endif
            }
        }
        internal interface ITrimSplit
        {
            IList<MemoryText> Split(MemoryText raw, CsvOptions options, int? capacity);
            MemoryText[] Trim(IList<MemoryText> fields, CsvOptions options);
        }

        internal readonly struct DefaultTrimSplit : ITrimSplit
        {
            public IList<MemoryText> Split(MemoryText raw, CsvOptions o, int? cap) => SplitLine(raw, o, cap);
            public MemoryText[] Trim(IList<MemoryText> f, CsvOptions o) => CsvReader.Trim(f, o);
        }

#if NET8_0_OR_GREATER
        internal readonly struct OptimizedTrimSplit : ITrimSplit
        {
            private readonly CsvMemoryOptions mem;
            public OptimizedTrimSplit(CsvMemoryOptions mem) => this.mem = mem;
            public IList<MemoryText> Split(MemoryText raw, CsvOptions o, int? cap) => SplitLineOptimized(raw, o, mem, cap);
            public MemoryText[] Trim(IList<MemoryText> f, CsvOptions o) => TrimOptimized(f, o, mem);
        }
#endif

        // One row type backs every read path. On net8 it implements all three frozen interfaces at once;
        // Headers/Values/Raw/this[] collide only by return type (string vs ReadOnlyMemory<char>), so the
        // memory-typed faces below are explicit interface implementations. Backing store is always MemoryText.
        internal sealed class CsvLine<TPolicy> :
                ICsvLine
#if NET8_0_OR_GREATER
                , ICsvLineSpan, ICsvLineFromMemory
#endif
            where TPolicy : struct, ITrimSplit
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
            private readonly TPolicy policy;
            private readonly MemoryText[] headers;
            private readonly MemoryText rawMemory;
            private readonly string? rawString;
            internal IList<MemoryText>? rawFields;
            internal MemoryText[]? parsedValues;

            private static readonly MemoryText Empty = "".AsMemory();

            internal CsvLine(MemoryText[] headers, Dictionary<string, int> headerLookup, int index,
                             MemoryText rawMemory, string? rawString, CsvOptions options, TPolicy policy = default)
            {
                this.headerLookup = headerLookup;
                this.options = options;
                this.policy = policy;
                this.headers = headers;
                this.rawMemory = rawMemory;
                this.rawString = rawString;
                Index = index;
            }

            public int Index { get; }

            public string Raw => rawString ?? rawMemory.ToString();

            public int ColumnCount => ParsedValues.Length;

            public bool HasColumn(string name) => headerLookup.ContainsKey(name);

            public bool LineHasColumn(string name)
                => headerLookup.TryGetValue(name, out var i) && RawFields.Count > i;

            public string[] Headers => headers.Select(h => h.AsString()).ToArray();

            public string[] Values => ParsedValues.Select(v => v.AsString()).ToArray();

            // headers.Length is a results-neutral presize hint for the field list; the split is identical without it.
            internal IList<MemoryText> RawFields => rawFields ??= policy.Split(rawMemory, options, headers.Length);

            private MemoryText[] ParsedValues
            {
                get
                {
                    if (parsedValues == null)
                    {
                        var raw = RawFields;

                        if (options.ValidateColumnCount && raw.Count != headers.Length)
                            throw new InvalidOperationException($"Expected {headers.Length}, got {raw.Count} columns.");

                        parsedValues = policy.Trim(raw, options);
                    }

                    return parsedValues;
                }
            }

            private MemoryText Get(string name)
            {
                if (!headerLookup.TryGetValue(name, out var i))
                {
                    if (options.ReturnEmptyForMissingColumn)
                        return Empty;

                    throw new ArgumentOutOfRangeException(nameof(name), name,
                        $"Header '{name}' does not exist. Expected one of {string.Join("; ", headers.Select(h => h.AsString()))}");
                }

                try
                {
                    return ParsedValues[i];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidOperationException(
                        $"Invalid row, missing {name} header, expected {headers.Length} columns, got {ParsedValues.Length} columns.");
                }
            }

            string ICsvLine.this[string name] => Get(name).AsString();
            string ICsvLine.this[int index] => ParsedValues[index].AsString();

            public override string ToString() => Raw;

#if NET8_0_OR_GREATER
            public ReadOnlyMemory<char>[] HeadersMemory => headers;
            public ReadOnlyMemory<char>[] ValuesMemory => ParsedValues;
            public ReadOnlyMemory<char> RawMemory => rawMemory;
            public ReadOnlySpan<char> RawSpan => rawMemory.Span;
            public ReadOnlySpan<char> HeadersSpan => throw new NotSupportedException("HeadersSpan not supported for array access. Use GetSpan(int) or GetMemory(int) for individual headers.");
            public ReadOnlySpan<char> ValuesSpan => throw new NotSupportedException("ValuesSpan not supported for array access. Use GetSpan(int) or GetMemory(int) for individual values.");

            public ReadOnlyMemory<char> GetMemory(string name) => Get(name);
            public ReadOnlyMemory<char> GetMemory(int index) => ParsedValues[index];

            public ReadOnlySpan<char> GetSpan(string name) => GetMemory(name).Span;
            public ReadOnlySpan<char> GetSpan(int index) => GetMemory(index).Span;

            public bool TryGetMemory(string name, out ReadOnlyMemory<char> value)
            {
                if (headerLookup.TryGetValue(name, out var index) && index < ParsedValues.Length)
                {
                    value = ParsedValues[index];
                    return true;
                }

                value = ReadOnlyMemory<char>.Empty;
                return false;
            }

            public bool TryGetMemory(int index, out ReadOnlyMemory<char> value)
            {
                if (index >= 0 && index < ParsedValues.Length)
                {
                    value = ParsedValues[index];
                    return true;
                }

                value = ReadOnlyMemory<char>.Empty;
                return false;
            }

            public bool TryGetSpan(string name, out ReadOnlySpan<char> value)
            {
                if (TryGetMemory(name, out var memory))
                {
                    value = memory.Span;
                    return true;
                }

                value = ReadOnlySpan<char>.Empty;
                return false;
            }

            public bool TryGetSpan(int index, out ReadOnlySpan<char> value)
            {
                if (TryGetMemory(index, out var memory))
                {
                    value = memory.Span;
                    return true;
                }

                value = ReadOnlySpan<char>.Empty;
                return false;
            }

            MemoryText[] ICsvLineFromMemory.Headers => headers;
            MemoryText[] ICsvLineFromMemory.Values => ParsedValues;
            MemoryText ICsvLineFromMemory.Raw => rawMemory;
            MemoryText ICsvLineFromMemory.this[string name] => Get(name);
            MemoryText ICsvLineFromMemory.this[int index] => ParsedValues[index];
#endif
        }

#if NET8_0_OR_GREATER

        private static IList<ReadOnlyMemory<char>> SplitLineOptimized(ReadOnlyMemory<char> line, CsvOptions options, CsvMemoryOptions memoryOptions, int? capacity = null)
        {
            var splitter = options.Splitter ?? CsvLineSplitter.Get(options);
            return splitter.Split(line, options, capacity);
        }

        private static ReadOnlyMemory<char>[] TrimOptimized(IList<ReadOnlyMemory<char>> line, CsvOptions options, CsvMemoryOptions memoryOptions)
        {
            var trimmed = new ReadOnlyMemory<char>[line.Count];

            for (var i = 0; i < line.Count; i++)
            {
                var str = line[i];

                if (options.TrimData)
                    str = TrimMemory(str);

                if (options.AllowEnclosedFieldValues && str.Length >= 2)
                {
                    var span = str.Span;
                    if (span[0] == '"' && span[^1] == '"')
                    {
                        str = str.Slice(1, str.Length - 2).Unescape('"', '"');

                        if (options.AllowBackSlashToEscapeQuote)
                            str = str.Unescape('\\', '"');
                    }
                    else if (options.AllowSingleQuoteToEncloseFieldValues && span[0] == '\'' && span[^1] == '\'')
                        str = str.Slice(1, str.Length - 2);
                }

                trimmed[i] = str;
            }

            return trimmed;
        }

        private static ReadOnlyMemory<char> TrimMemory(ReadOnlyMemory<char> memory)
        {
            var span = memory.Span;
            int start = 0;
            int end = span.Length - 1;

            while (start <= end && char.IsWhiteSpace(span[start]))
                start++;

            while (end >= start && char.IsWhiteSpace(span[end]))
                end--;

            if (start > end)
                return ReadOnlyMemory<char>.Empty;

            return memory.Slice(start, end - start + 1);
        }

#endif
    }
}