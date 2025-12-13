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

#if NET8_0_OR_GREATER

        /// <summary>
        /// Reads the lines from the reader with enhanced Span/Memory support.
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
        /// Reads the lines from the stream with enhanced Span/Memory support.
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
        /// Reads the lines from the csv string with enhanced Span/Memory support.
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
        {
            options ??= new CsvOptions();

            string? line;
            var index = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;
            while ((line = reader.ReadLine()) != null)
            {
                index++;

                var lineAsMemory = line.AsMemory();
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(lineAsMemory, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(lineAsMemory.AsSpan(), options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    // For HeaderAbsent mode with multiline fields, we need to process the complete line first
                    if (!skipInitialLine && options.AllowNewLineInEnclosedFieldValues)
                    {
                        // Process multiline fields to get the complete first data line
                        var completeLineForHeaders = line;
                        var tempSplitter = CsvLineSplitter.Get(options);
                        var splitLine = tempSplitter.Split(lineAsMemory, options);
                        
                        while (splitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
                        {
                            var nextLine = reader.ReadLine();
                            if (nextLine == null)
                                break;
                                
                            completeLineForHeaders = StringHelpers.Concat(completeLineForHeaders.AsMemory(), options.NewLine, nextLine.AsMemory()).AsString();
                            lineAsMemory = completeLineForHeaders.AsMemory();
                            splitLine = tempSplitter.Split(lineAsMemory, options);
                        }
                        
                        // Update line to the complete multiline version
                        line = completeLineForHeaders;
                        lineAsMemory = line.AsMemory();
                    }

                    headers = skipInitialLine ? GetHeaders(lineAsMemory, options) : CreateDefaultHeaders(lineAsMemory, options);

                    try
                    {
                        headerLookup = CreateHeaderLookup(headers, options);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException("Duplicate headers detected in HeaderPresent mode. If you don't have a header you can set the HeaderMode to HeaderAbsent.");
                    }

                    var aliases = options.Aliases;
                    if (aliases != null)
                    {
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

                var record = new ReadLineSpan(headers, headerLookup, index, line, options);
                // Only process multiline if we haven't already done it for header creation
                var isFirstDataLineInHeaderAbsentMode = (headers != null && options.HeaderMode == HeaderMode.HeaderAbsent && 
                                                         record.Index == (options.RowsToSkip + 1));
                if (options.AllowNewLineInEnclosedFieldValues && !isFirstDataLineInHeaderAbsentMode)
                {
                    while (record.RawSplitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
                    {
                        var nextLine = reader.ReadLine();
                        if (nextLine == null)
                            break;

                        line = StringHelpers.Concat(line.AsMemory(), options.NewLine, nextLine.AsMemory()).AsString();
                        record = new ReadLineSpan(headers, headerLookup, index, line, options);
                    }
                }

                yield return record;
            }
        }

        /// <summary>
        /// Reads CSV data from memory with enhanced memory management options.
        /// </summary>
        /// <param name="csv">The CSV data as ReadOnlyMemory.</param>
        /// <param name="options">The CSV parsing options.</param>
        /// <param name="memoryOptions">The memory management options.</param>
        /// <returns>An enumerable of CSV lines with memory optimization.</returns>
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
        {
            var position = 0;
            var index = 0;
            ReadOnlyMemory<char>[]? headers = null;
            Dictionary<string, int>? headerLookup = null;

            while (position < csv.Length)
            {
                var line = ReadLineOptimized(csv, ref position, memoryOptions);
                if (line.IsEmpty) break;

                index++;

                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(line.Span, options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(line, options) : CreateDefaultHeaders(line, options);

                    try
                    {
                        headerLookup = CreateHeaderLookup(headers, options);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException("Duplicate headers detected in HeaderPresent mode. If you don't have a header you can set the HeaderMode to HeaderAbsent.");
                    }

                    if (skipInitialLine)
                        continue;
                }

                var record = new ReadLineSpanOptimized(headers, headerLookup, index, line, options, memoryOptions);
                if (options.AllowNewLineInEnclosedFieldValues)
                {
                    while (record.RawSplitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.Span, options)))
                    {
                        var nextLine = ReadLineOptimized(csv, ref position, memoryOptions);
                        if (nextLine.IsEmpty)
                            break;

                        line = ConcatenateMemory(line, options.NewLine.AsMemory(), nextLine, memoryOptions);
                        record = new ReadLineSpanOptimized(headers, headerLookup, index, line, options, memoryOptions);
                    }
                }

                yield return record;
            }
        }

        private static ReadOnlyMemory<char> ReadLineOptimized(ReadOnlyMemory<char> source, ref int position, CsvMemoryOptions memoryOptions)
        {
            if (position >= source.Length)
                return ReadOnlyMemory<char>.Empty;

            var span = source.Span.Slice(position);
            var newlineIndex = span.IndexOfAny('\n', '\r');
            
            if (newlineIndex == -1)
            {
                // Last line without newline
                var result = source.Slice(position);
                position = source.Length;
                return result;
            }

            var lineLength = newlineIndex;
            var line = source.Slice(position, lineLength);
            
            // Skip newline characters
            position += lineLength;
            if (position < source.Length)
            {
                var ch = source.Span[position];
                if (ch == '\r' || ch == '\n')
                {
                    position++;
                    // Handle CRLF
                    if (position < source.Length && ch == '\r' && source.Span[position] == '\n')
                        position++;
                }
            }

            return line;
        }

        private static ReadOnlyMemory<char> ConcatenateMemory(ReadOnlyMemory<char> first, ReadOnlyMemory<char> separator, ReadOnlyMemory<char> second, CsvMemoryOptions memoryOptions)
        {
            var totalLength = first.Length + separator.Length + second.Length;
            var buffer = memoryOptions.CharArrayPool.Rent(totalLength);
            
            try
            {
                var span = buffer.AsSpan();
                first.Span.CopyTo(span);
                separator.Span.CopyTo(span.Slice(first.Length));
                second.Span.CopyTo(span.Slice(first.Length + separator.Length));
                
                var result = new char[totalLength];
                span.Slice(0, totalLength).CopyTo(result);
                return result.AsMemory();
            }
            finally
            {
                memoryOptions.CharArrayPool.Return(buffer);
            }
        }

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
        {
            // NOTE: Logic is copied in ReadImpl/ReadImplAsync/ReadFromMemory
            options ??= new CsvOptions();

            string? line;
            var index = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;
            while ((line = reader.ReadLine()) != null)
            {
                index++;

                var lineAsMemory = line.AsMemory();
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(lineAsMemory, index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(lineAsMemory.AsSpan(), options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    // For HeaderAbsent mode with multiline fields, we need to process the complete line first
                    if (!skipInitialLine && options.AllowNewLineInEnclosedFieldValues)
                    {
                        // Process multiline fields to get the complete first data line
                        var completeLineForHeaders = line;
                        var tempSplitter = CsvLineSplitter.Get(options);
                        var splitLine = tempSplitter.Split(lineAsMemory, options);
                        
                        while (splitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
                        {
                            var nextLine = reader.ReadLine();
                            if (nextLine == null)
                                break;
                                
                            completeLineForHeaders += options.NewLine + nextLine;
                            lineAsMemory = completeLineForHeaders.AsMemory();
                            splitLine = tempSplitter.Split(lineAsMemory, options);
                        }
                        
                        // Update line to the complete multiline version
                        line = completeLineForHeaders;
                        lineAsMemory = line.AsMemory();
                    }

                    headers = skipInitialLine ? GetHeaders(lineAsMemory, options) : CreateDefaultHeaders(lineAsMemory, options);

                    try
                    {
                        headerLookup = CreateHeaderLookup(headers, options);
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
                // Only process multiline if we haven't already done it for header creation
                var isFirstDataLineInHeaderAbsentMode = (headers != null && options.HeaderMode == HeaderMode.HeaderAbsent && 
                                                         record.Index == (options.RowsToSkip + 1));
                if (options.AllowNewLineInEnclosedFieldValues && !isFirstDataLineInHeaderAbsentMode)
                {
                    // TODO: Move to CsvLineSplitter?
                    // TODO: Shouldn't we only check the last part?
                    while (record.RawSplitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
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

#if NET8_0_OR_GREATER
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
                using var reader = new StringReader(csv);
                await foreach (var line in ReadImplAsync(reader, options))
                    yield return line;
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
                        headerLookup = CreateHeaderLookup(headers, options);
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
                    while (record.RawSplitLine.Any(f => CsvLineSplitter.IsUnterminatedQuotedValue(f.AsSpan(), options)))
                    {
                        var nextLine = await reader.ReadLineAsync();
                        if (nextLine == null)
                            break;

                        line += options.NewLine + nextLine;
                        record = new ReadLine(headers, headerLookup, index, line, options);
                    }
                }

                yield return record;
            }
        }
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

            public bool LineHasColumn(string name)
            {
                if (!headerLookup.TryGetValue(name, out var index))
                    return false;

                return RawSplitLine.Count > index;
            }

            internal IList<MemoryText> RawSplitLine
            {
                get
                {
#if NET8_0_OR_GREATER
                    rawSplitLine ??= SplitLine(Raw.AsMemory(), options, headers.Length);
#else
                    rawSplitLine ??= SplitLine(Raw, options, headers.Length);
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

#if NET8_0_OR_GREATER

        private sealed class ReadLineSpan : ICsvLineSpan
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
            private readonly MemoryText[] headers;
            private IList<MemoryText>? rawSplitLine;
            internal MemoryText[]? parsedLine;

            public ReadLineSpan(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, string raw, CsvOptions options)
            {
                this.headerLookup = headerLookup;
                this.options = options;
                this.headers = headers;
                Raw = raw;
                Index = index;
            }

            public string[] Headers => headers.Select(it => it.AsString()).ToArray();
            public ReadOnlyMemory<char>[] HeadersMemory => headers;
            public ReadOnlySpan<char> HeadersSpan => throw new NotSupportedException("HeadersSpan not supported for array access. Use GetSpan(int) or GetMemory(int) for individual headers.");

            public string Raw { get; }
            public ReadOnlyMemory<char> RawMemory => Raw.AsMemory();
            public ReadOnlySpan<char> RawSpan => Raw.AsSpan();

            public int Index { get; }
            public int ColumnCount => Line.Length;

            public bool HasColumn(string name) => headerLookup.ContainsKey(name);

            public bool LineHasColumn(string name)
            {
                if (!headerLookup.TryGetValue(name, out var index))
                    return false;

                return RawSplitLine.Count > index;
            }

            internal IList<MemoryText> RawSplitLine => rawSplitLine ??= SplitLine(Raw.AsMemory(), options, headers.Length);

            public string[] Values => Line.Select(it => it.AsString()).ToArray();
            public ReadOnlyMemory<char>[] ValuesMemory => Line;
            public ReadOnlySpan<char> ValuesSpan => throw new NotSupportedException("ValuesSpan not supported for array access. Use GetSpan(int) or GetMemory(int) for individual values.");

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

            string ICsvLine.this[string name] => GetSpan(name).ToString();
            string ICsvLine.this[int index] => GetSpan(index).ToString();

            public ReadOnlyMemory<char> GetMemory(string name)
            {
                if (!headerLookup.TryGetValue(name, out var index))
                {
                    if (options.ReturnEmptyForMissingColumn)
                        return ReadOnlyMemory<char>.Empty;

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

            public ReadOnlyMemory<char> GetMemory(int index) => Line[index];

            public ReadOnlySpan<char> GetSpan(string name) => GetMemory(name).Span;
            public ReadOnlySpan<char> GetSpan(int index) => GetMemory(index).Span;

            public bool TryGetMemory(string name, out ReadOnlyMemory<char> value)
            {
                if (headerLookup.TryGetValue(name, out var index) && index < Line.Length)
                {
                    value = Line[index];
                    return true;
                }

                value = ReadOnlyMemory<char>.Empty;
                return false;
            }

            public bool TryGetMemory(int index, out ReadOnlyMemory<char> value)
            {
                if (index >= 0 && index < Line.Length)
                {
                    value = Line[index];
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

            public override string ToString() => Raw;
        }

        private sealed class ReadLineSpanOptimized : ICsvLineSpan
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
            private readonly CsvMemoryOptions memoryOptions;
            private readonly ReadOnlyMemory<char>[] headers;
            private readonly ReadOnlyMemory<char> rawMemory;
            private IList<ReadOnlyMemory<char>>? rawSplitLine;
            private ReadOnlyMemory<char>[]? parsedLine;

            public ReadLineSpanOptimized(ReadOnlyMemory<char>[] headers, Dictionary<string, int> headerLookup, int index, ReadOnlyMemory<char> raw, CsvOptions options, CsvMemoryOptions memoryOptions)
            {
                this.headerLookup = headerLookup;
                this.options = options;
                this.memoryOptions = memoryOptions;
                this.headers = headers;
                this.rawMemory = raw;
                Index = index;
            }

            public string[] Headers => headers.Select(h => h.ToString()).ToArray();
            public ReadOnlyMemory<char>[] HeadersMemory => headers;
            public ReadOnlySpan<char> HeadersSpan => throw new NotSupportedException("HeadersSpan not supported for array access. Use GetSpan(int) or GetMemory(int) for individual headers.");

            public string Raw => rawMemory.ToString();
            public ReadOnlyMemory<char> RawMemory => rawMemory;
            public ReadOnlySpan<char> RawSpan => rawMemory.Span;

            public int Index { get; }
            public int ColumnCount => Line.Length;

            public bool HasColumn(string name) => headerLookup.ContainsKey(name);

            public bool LineHasColumn(string name)
            {
                if (!headerLookup.TryGetValue(name, out var index))
                    return false;

                return RawSplitLine.Count > index;
            }

            internal IList<ReadOnlyMemory<char>> RawSplitLine => rawSplitLine ??= SplitLineOptimized(rawMemory, options, memoryOptions);

            public string[] Values => Line.Select(v => v.ToString()).ToArray();
            public ReadOnlyMemory<char>[] ValuesMemory => Line;
            public ReadOnlySpan<char> ValuesSpan => throw new NotSupportedException("ValuesSpan not supported for array access. Use GetSpan(int) or GetMemory(int) for individual values.");

            private ReadOnlyMemory<char>[] Line
            {
                get
                {
                    if (parsedLine == null)
                    {
                        var raw = RawSplitLine;

                        if (options.ValidateColumnCount && raw.Count != Headers.Length)
                            throw new InvalidOperationException($"Expected {Headers.Length}, got {raw.Count} columns.");

                        parsedLine = TrimOptimized(raw, options, memoryOptions);
                    }

                    return parsedLine;
                }
            }

            string ICsvLine.this[string name] => GetSpan(name).ToString();
            string ICsvLine.this[int index] => GetSpan(index).ToString();

            public ReadOnlyMemory<char> GetMemory(string name)
            {
                if (!headerLookup.TryGetValue(name, out var index))
                {
                    if (options.ReturnEmptyForMissingColumn)
                        return ReadOnlyMemory<char>.Empty;

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

            public ReadOnlyMemory<char> GetMemory(int index) => Line[index];

            public ReadOnlySpan<char> GetSpan(string name) => GetMemory(name).Span;
            public ReadOnlySpan<char> GetSpan(int index) => GetMemory(index).Span;

            public bool TryGetMemory(string name, out ReadOnlyMemory<char> value)
            {
                if (headerLookup.TryGetValue(name, out var index) && index < Line.Length)
                {
                    value = Line[index];
                    return true;
                }

                value = ReadOnlyMemory<char>.Empty;
                return false;
            }

            public bool TryGetMemory(int index, out ReadOnlyMemory<char> value)
            {
                if (index >= 0 && index < Line.Length)
                {
                    value = Line[index];
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

            public override string ToString() => Raw;
        }

        private static IList<ReadOnlyMemory<char>> SplitLineOptimized(ReadOnlyMemory<char> line, CsvOptions options, CsvMemoryOptions memoryOptions, int? capacity = null)
        {
            var splitter = CsvLineSplitter.Get(options);
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