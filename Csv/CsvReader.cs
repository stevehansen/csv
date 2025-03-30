using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
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
        /// Reads the lines from the reader.
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
        /// Reads the lines from the stream.
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
        /// Reads the lines from the csv string.
        /// </summary>
        /// <param name="csv">The csv string to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLine> ReadFromText(string csv, CsvOptions? options = null)
        {
            if (csv == null)
                throw new ArgumentNullException(nameof(csv));

            return ReadFromTextImpl(csv, options);
        }

        private static IEnumerable<ICsvLine> ReadFromStreamImpl(Stream stream, CsvOptions? options)
        {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
            // Temporarily disabling the optimized stream approach
            /*
            if (stream.Length > 1024 * 1024) // 1MB threshold
            {
                // Optimized implementation here...
            }
            */
#endif

            using (var reader = new StreamReader(stream))
            {
                foreach (var line in ReadImpl(reader, options))
                    yield return line;
            }
        }

        private static IEnumerable<ICsvLine> ReadFromTextImpl(string csv, CsvOptions? options)
        {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
            // Temporarily use the standard TextReader approach to ensure compatibility
            using (var reader = new StringReader(csv))
            {
                foreach (var line in ReadImpl(reader, options))
                    yield return line;
            }
#else
            using (var reader = new StringReader(csv))
            {
                foreach (var line in ReadImpl(reader, options))
                    yield return line;
            }
#endif
        }

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
        /// <summary>
        /// Adapter to convert ICsvLineFromMemory to ICsvLine
        /// </summary>
        private sealed class MemoryToStringLine : ICsvLine
        {
            private readonly ICsvLineFromMemory memoryLine;

            public MemoryToStringLine(ICsvLineFromMemory memoryLine)
            {
                this.memoryLine = memoryLine;
            }

            public string[] Headers => memoryLine.Headers.Select(h => h.ToString()).ToArray();

            public string Raw => memoryLine.Raw.ToString();

            public int Index => memoryLine.Index;

            public int ColumnCount => memoryLine.ColumnCount;

            public bool HasColumn(string name) => memoryLine.HasColumn(name);

            public string[] Values => memoryLine.Values.Select(v => v.ToString()).ToArray();

            public string this[string name] => memoryLine[name].ToString();

            public string this[int index] => memoryLine[index].ToString();
        }
#endif

        private static IEnumerable<ICsvLine> ReadImpl(TextReader reader, CsvOptions? options)
        {
            // NOTE: Logic is copied in ReadImpl/ReadImplAsync/ReadFromMemory
            options ??= new CsvOptions();

            string? line;
            var index = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
            // Temporarily removing optimized buffering for compatibility
            while ((line = reader.ReadLine()) != null)
            {
#else
            while ((line = reader.ReadLine()) != null)
            {
#endif
                index++;

                var lineAsMemory = line.AsMemory();
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(lineAsMemory, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(lineAsMemory.AsSpan(), options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(lineAsMemory, options) : CreateDefaultHeaders(lineAsMemory, options);

                    try
                    {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
                        headerLookup = headers
                            .Select((h, idx) => (h, idx))
                            .ToDictionary(h => h.Item1.AsString(), h => h.Item2, options.Comparer);
#else
                        headerLookup = headers
                            .Select((h, idx) => Tuple.Create(h, idx))
                            .ToDictionary(h => h.Item1.AsString(), h => h.Item2, options.Comparer);
#endif
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

                var record = new ReadLine(headers, headerLookup, index, line, options);
                if (options.AllowNewLineInEnclosedFieldValues)
                {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
                    // Using the original implementation for now
                    while (record.RawSplitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
#else
                    // TODO: Move to CsvLineSplitter?
                    // TODO: Shouldn't we only check the last part?
                    while (record.RawSplitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
#endif
                    {
                        var nextLine = reader.ReadLine();
                        if (nextLine == null)
                            break;

                        line += options.NewLine + nextLine;
                        record = new ReadLine(headers, headerLookup, index, line, options);

                    }
                }

                yield return record;
            }
        }

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
        /// <summary>
        /// Reads the lines from the reader.
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
        /// Reads the lines from the stream.
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
        /// Reads the lines from the csv string.
        /// </summary>
        /// <param name="csv">The csv string to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IAsyncEnumerable<ICsvLine> ReadFromTextAsync(string csv, CsvOptions? options = null)
        {
            if (csv == null)
                throw new ArgumentNullException(nameof(csv));

            static async IAsyncEnumerable<ICsvLine> Impl(string csv, CsvOptions? options)
            {
                // For string-based CSV, we can use the faster memory-based approach
                foreach (var line in ReadFromMemory(csv.AsMemory(), options))
                {
                    yield return new MemoryToStringLine(line);
                }
            }

            return Impl(csv, options);
        }

        private static async IAsyncEnumerable<ICsvLine> ReadImplAsync(TextReader reader, CsvOptions? options)
        {
            // NOTE: Logic is copied in ReadImpl/ReadImplAsync/ReadFromMemory
            options ??= new CsvOptions();

            string? line;
            var index = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                index++;

                var lineAsMemory = line.AsMemory();
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(lineAsMemory, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(lineAsMemory.Span, options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(lineAsMemory, options) : CreateDefaultHeaders(lineAsMemory, options);

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

                var record = new ReadLine(headers, headerLookup, index, line, options);
                if (options.AllowNewLineInEnclosedFieldValues)
                {
                    // Optimized check for unterminated quotes
                    bool hasUnterminatedQuote = false;
                    foreach (var field in record.RawSplitLine)
                    {
                        if (CsvLineSplitter.IsUnterminatedQuotedValue(field.AsSpan(), options))
                        {
                            hasUnterminatedQuote = true;
                            break;
                        }
                    }

                    while (hasUnterminatedQuote)
                    {
                        var nextLine = await reader.ReadLineAsync();
                        if (nextLine == null)
                            break;

                        line += options.NewLine + nextLine;
                        record = new ReadLine(headers, headerLookup, index, line, options);

                        // Recheck for unterminated quotes
                        hasUnterminatedQuote = false;
                        foreach (var field in record.RawSplitLine)
                        {
                            if (CsvLineSplitter.IsUnterminatedQuotedValue(field.AsSpan(), options))
                            {
                                hasUnterminatedQuote = true;
                                break;
                            }
                        }
                    }
                }

                yield return record;
            }
        }
#endif

        private static char AutoDetectSeparator(SpanText sampleLine)
        {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
            // Try to determine the best separator by analyzing frequency
            ReadOnlySpan<char> candidates = stackalloc char[] { ',', ';', '\t', '|' };

            // Count occurrences for each candidate
            Span<int> counts = stackalloc int[candidates.Length];

            for (int i = 0; i < sampleLine.Length; i++)
            {
                for (int j = 0; j < candidates.Length; j++)
                {
                    if (sampleLine[i] == candidates[j])
                    {
                        counts[j]++;
                    }
                }
            }

            // Find the most common separator
            int maxCount = 0;
            int maxIndex = -1;

            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > maxCount)
                {
                    maxCount = counts[i];
                    maxIndex = i;
                }
            }

            if (maxCount > 0)
            {
                return candidates[maxIndex];
            }

            return ','; // Default if nothing found
#else
            // NOTE: Try simple 'detection' of possible separator
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var ch in sampleLine)
            {
                if (ch == ';' || ch == '\t')
                    return ch;
            }

            return ',';
#endif
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

        private static void InitializeOptions(SpanText line, CsvOptions options)
        {
            if (options.Separator == '\0')
                options.Separator = AutoDetectSeparator(line);

            options.Splitter = CsvLineSplitter.Get(options);
        }

        private static IList<MemoryText> SplitLine(MemoryText line, CsvOptions options)
        {
            return options.Splitter.Split(line, options);
        }

        private static MemoryText[] Trim(IList<MemoryText> line, CsvOptions options)
        {
            var trimmed = new MemoryText[line.Count]; // TODO: Mutate existing array?
            for (var i = 0; i < line.Count; i++)
            {
                var str = line[i];
                if (options.TrimData)
                    str = str.Trim();

                if (str.Length > 1)
                {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
                    if (str.Span[0] == '"' && str.Span[^1] == '"')
                    {
                        str = str[1..^1].Unescape('"', '"');

                        if (options.AllowBackSlashToEscapeQuote)
                            str = str.Unescape('\\', '"');
                    }
                    else if (options.AllowSingleQuoteToEncloseFieldValues && str.Span[0] == '\'' && str.Span[^1] == '\'')
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
                    headers = line.Headers.Skip(start).Select(x => x.AsMemory()).ToArray();
                else
                    headers = line.Headers.Skip(start).Take(length).Select(x => x.AsMemory()).ToArray();
                MemoryText[] values = headers.Select(x => line[x.ToString()].AsMemory()).ToArray();
                Dictionary<string, int> map = Enumerable.Range(0, headers.Length).ToDictionary(x => headers[x].ToString());
                return new ReadLine(headers, map, line.Index, line.Raw, new CsvOptions()) { parsedLine = values };
            }
        }
        private sealed class ReadLine : ICsvLine
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
            private readonly MemoryText[] headers;
            private IList<MemoryText>? rawSplitLine;
            internal MemoryText[]? parsedLine;

            public ReadLine(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, string raw, CsvOptions options)
            {
                this.headerLookup = headerLookup;
                this.options = options;
                this.headers = headers;
                Raw = raw;
                Index = index;
            }

            public string[] Headers => headers.Select(it => it.AsString()).ToArray();

            public string Raw { get; }

            public int Index { get; }

            public int ColumnCount => Line.Length;

            public bool HasColumn(string name) => headerLookup.ContainsKey(name);

            internal IList<MemoryText> RawSplitLine
            {
                get
                {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
                    if (rawSplitLine == null)
                    {
                        // Use optimized span splitter
                        rawSplitLine = options.Splitter.Split(Raw.AsMemory(), options);
                    }
#else
                    rawSplitLine ??= SplitLine(Raw, options);
#endif
                    return rawSplitLine;
                }
            }

            public string[] Values => Line.Select(it => it.AsString()).ToArray();

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

            string ICsvLine.this[string name]
            {
                get
                {
                    if (!headerLookup.TryGetValue(name, out var index))
                    {
                        if (options.ReturnEmptyForMissingColumn)
                            return string.Empty;

                        throw new ArgumentOutOfRangeException(nameof(name), name, $"Header '{name}' does not exist. Expected one of {string.Join("; ", Headers)}");
                    }

                    try
                    {
                        return Line[index].AsString();
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw new InvalidOperationException($"Invalid row, missing {name} header, expected {Headers.Length} columns, got {Line.Length} columns.");
                    }
                }
            }

            string ICsvLine.this[int index] => Line[index].AsString();

            public override string ToString()
            {
                return Raw;
            }
        }
    }
}