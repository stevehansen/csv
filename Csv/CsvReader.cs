using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if NETCOREAPP3_1 || NETSTANDARD2_1
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
                
#if NETCOREAPP3_1 || NETSTANDARD2_1
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line.AsMemory(), index) == true)
#else
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line, index) == true)
#endif
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(line, options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(line.AsMemory(), options) : CreateDefaultHeaders(line, options);

                    try
                    {
                        headerLookup = headers
                            .Select((h, idx) => Tuple.Create(h, idx))
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
                    while (record.RawSplitLine.Any(f => IsUnterminatedQuotedValue(f.AsSpan(), options)))
                    {
                        var nextLine = reader.ReadLine();
                        if (nextLine == null)
                        {
                            break;
                        }
                        line += options.NewLine + nextLine;
                        record = new ReadLine(headers, headerLookup, index, line, options);
                    }
                }

                yield return record;
            }
        }

#if NETCOREAPP3_1 || NETSTANDARD2_1
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
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line.AsMemory(), index) == true)
                    continue;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(line, options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(line.AsMemory(), options) : CreateDefaultHeaders(line, options);

                    try
                    {
                        headerLookup = headers
                            .Select((h, idx) => Tuple.Create(h, idx))
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
                    while (record.RawSplitLine.Any(f => IsUnterminatedQuotedValue(f.AsSpan(), options)))
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
            foreach (var ch in sampleLine)
            {
                if (ch == ';' || ch == '\t')
                    return ch;
            }

            return ',';
        }

        private static MemoryText[] CreateDefaultHeaders(SpanText line, CsvOptions options)
        {
            var columnCount = options.Splitter.MatchesCount(line);

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

        private static MemoryText[] SplitLine(MemoryText line, CsvOptions options)
        {
            var matches = options.Splitter.Matches(line);
            var parts = new List<MemoryText>(matches.Length);
            var p = -1;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < matches.Length; i++)
            {
                var value = matches[i];
                if (p >= 0 && IsUnterminatedQuotedValue(parts[p].AsSpan(), options))
                {
                    parts[p] = StringHelpers.Concat(parts[p], options.Separator.ToString(), value);
                }
                else
                {
                    parts.Add(value);
                    p++;
                }
            }

            return parts.ToArray();
        }

        private static MemoryText[] Trim(MemoryText[] line, CsvOptions options)
        {
            var trimmed = new MemoryText[line.Length];
            for (var i = 0; i < line.Length; i++)
            {
                var str = line[i];
                if (options.TrimData)
                    str = str.Trim();

                if (str.StartsWith("\"") && str.EndsWith("\"") && str.Length > 1)
                {
#if NETCOREAPP3_1 || NETSTANDARD2_1
                    str = str[1..^1].Replace("\"\"", "\"");
#else
                    str = str.Substring(1, str.Length - 2).Replace("\"\"", "\"");
#endif

                    if (options.AllowBackSlashToEscapeQuote)
                        str = str.Replace("\\\"", "\"");
                }
                else if (options.AllowSingleQuoteToEncloseFieldValues && str.StartsWith("'") && str.EndsWith("'") && str.Length > 1)
#if NETCOREAPP3_1 || NETSTANDARD2_1
                    str = str[1..^1];
#else
                    str = str.Substring(1, str.Length - 2);
#endif

                trimmed[i] = str;
            }

            return trimmed;
        }

        private static bool IsUnterminatedQuotedValue(SpanText value, CsvOptions options)
        {
            if (value.Length == 0)
                return false;
            
            char quoteChar;
            if (value[0] == '"')
            {
                quoteChar = '"';
            }
            else if (options.AllowSingleQuoteToEncloseFieldValues && value[0] == '\'')
            {
                quoteChar = '\'';
            }
            else
            {
                return false;
            }

#if NETCOREAPP3_1 || NETSTANDARD2_1
            var trailingQuotes = StringHelpers.RegexMatch(value[1..], $@"\\?{quoteChar}+$");
#else
            var trailingQuotes = StringHelpers.RegexMatch(value.Substring(1), $@"\\?{quoteChar}+$");
#endif
            // if the first trailing quote is escaped, ignore it
            if (options.AllowBackSlashToEscapeQuote && trailingQuotes.StartsWith("\\"))
            {
                trailingQuotes = trailingQuotes.Substring(2);
            }
            // the value is properly terminated if there are an odd number of unescaped quotes at the end
            return trailingQuotes.Length % 2 == 0;
        }

        private sealed class ReadLine : ICsvLine
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
            private readonly MemoryText[] headers;
            private MemoryText[]? rawSplitLine;
            private MemoryText[]? parsedLine;

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

            internal MemoryText[] RawSplitLine
            {
                get
                {
                    if (rawSplitLine == null)
                    {
                        lock (headerLookup)
                        {
#if NETCOREAPP3_1 || NETSTANDARD2_1
                            rawSplitLine ??= SplitLine(Raw.AsMemory(), options);
#else
                            rawSplitLine ??= SplitLine(Raw, options);
#endif
                        }
                    }
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

                        if (options.ValidateColumnCount && raw.Length != Headers.Length)
                            throw new InvalidOperationException($"Expected {Headers.Length}, got {raw.Length} columns.");

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