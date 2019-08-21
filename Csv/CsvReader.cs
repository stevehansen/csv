using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Csv
{
    /// <summary>
    /// Helper class to read csv (comma separated values) data.
    /// </summary>
    public static class CsvReader
    {
        private static readonly Dictionary<Tuple<char, bool>, Regex> splitterCache = new Dictionary<Tuple<char, bool>, Regex>();
        private static readonly object syncRoot = new object();

        /// <summary>
        /// Reads the lines from the reader.
        /// </summary>
        /// <param name="reader">The text reader to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLine> Read(TextReader reader, CsvOptions options = null)
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
        public static IEnumerable<ICsvLine> ReadFromStream(Stream stream, CsvOptions options = null)
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
        public static IEnumerable<ICsvLine> ReadFromText(string csv, CsvOptions options = null)
        {
            if (csv == null)
                throw new ArgumentNullException(nameof(csv));

            return ReadFromTextImpl(csv, options);
        }

        private static IEnumerable<ICsvLine> ReadFromStreamImpl(Stream stream, CsvOptions options)
        {
            using (var reader = new StreamReader(stream))
            {
                foreach (var line in ReadImpl(reader, options))
                    yield return line;
            }
        }

        private static IEnumerable<ICsvLine> ReadFromTextImpl(string csv, CsvOptions options)
        {
            using (var reader = new StringReader(csv))
            {
                foreach (var line in ReadImpl(reader, options))
                    yield return line;
            }
        }

        private static IEnumerable<ICsvLine> ReadImpl(TextReader reader, CsvOptions options)
        {
            if (options == null)
                options = new CsvOptions();

            string line;
            var index = 0;
            string[] headers = null;
            Dictionary<string, int> headerLookup = null;
            var initalized = false;
            while ((line = reader.ReadLine()) != null)
            {
                index++;
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line, index) == true)
                    continue;

                if (!initalized)
                {
                    InitializeOptions(line, options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    headers = skipInitialLine ? GetHeaders(line, options) : CreateDefaultHeaders(line, options);

                    try
                    {
                        headerLookup = headers.Select((h, idx) => Tuple.Create(h, idx)).ToDictionary(h => h.Item1, h => h.Item2, options.Comparer);
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

                    initalized = true;

                    if (skipInitialLine)
                        continue;
                }

                var record = new ReadLine(headers, headerLookup, index, line, options);
                if (options.AllowNewLineInEnclosedFieldValues)
                {
                    while (record.RawSplitLine.Any(f => IsUnterminatedQuotedValue(f, options)))
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

        private static char AutoDetectSeparator(string sampleLine)
        {
            // NOTE: Try simple 'detection' of possible separator
            foreach (var ch in sampleLine)
            {
                if (ch == ';' || ch == '\t')
                    return ch;
            }

            return ',';
        }

        private static string[] CreateDefaultHeaders(string line, CsvOptions options)
        {
            var columnCount = options.Splitter.Matches(line);

            var headers = new string[columnCount.Count];

            for (var i = 0; i < headers.Length; i++)
                headers[i] = $"Column{i + 1}";

            return headers;
        }

        private static string[] GetHeaders(string line, CsvOptions options)
        {
            return Trim(SplitLine(line, options), options);
        }

        private static void InitializeOptions(string line, CsvOptions options)
        {
            if (options.Separator == '\0')
                options.Separator = AutoDetectSeparator(line);


            Regex splitter;
            lock (syncRoot)
            {
                var key = new Tuple<char, bool>(options.Separator, options.AllowSingleQuoteToEncloseFieldValues);
                if (!splitterCache.TryGetValue(key, out splitter))
                    splitterCache[key] = splitter = CreateRegex(options);
            }

            options.Splitter = splitter;
        }

        private static Regex CreateRegex(CsvOptions options)
        {
            var pattern = @"(?>(?(IQ)(?(ESC).(?<-ESC>)|\\(?<ESC>))|(?!))|(?(IQ)\k<QUOTE>(?<-IQ>)|(?<=^|{0})(?<QUOTE>[{1}])(?<IQ>))|(?(IQ).|[^{0}]))+|^(?={0})|(?<={0})(?={0})|(?<={0})$";
            var separator = Regex.Escape(options.Separator.ToString());
            var quoteChars = options.AllowSingleQuoteToEncloseFieldValues ? "\"'" : "\"";
            // Since netstandard1.0 doesn't include RegexOptions.Compiled, we include it by value (in case the target platform supports it)
            var regexOptions = RegexOptions.Singleline | ((RegexOptions/*.Compiled*/)8);
            return new Regex(string.Format(pattern, separator, quoteChars), regexOptions);
        }

        private static string[] SplitLine(string line, CsvOptions options)
        {
            var matches = options.Splitter.Matches(line);
            return matches.Cast<Match>()
                .Select(m => m.Value)
                .ToArray();
        }

        private static string[] Trim(string[] line, CsvOptions options)
        {
            return line.Select(str =>
            {
                if (options.TrimData)
                    str = str.Trim();

                if (str.StartsWith("\"") && str.EndsWith("\"") && str.Length > 1)
                {
                    str = str.Substring(1, str.Length - 2).Replace("\"\"", "\"");
                    if (options.AllowBackSlashToEscapeQuote)
                    {
                        str = str.Replace("\\\"", "\"");
                    }
                }
                else if (options.AllowSingleQuoteToEncloseFieldValues && str.StartsWith("'") && str.EndsWith("'") && str.Length > 1)
                {
                    str = str.Substring(1, str.Length - 2);
                }

                return str;
            }).ToArray();
        }

        private static bool IsUnterminatedQuotedValue(string value, CsvOptions options)
        {
            char quoteChar;
            if (value.StartsWith("\""))
            {
                quoteChar = '"';
            }
            else if (options.AllowSingleQuoteToEncloseFieldValues && value.StartsWith("'"))
            {
                quoteChar = '\'';
            }
            else
            {
                return false;
            }

            var trailingQuotes = Regex.Match(value, $@"\\?{quoteChar}+$").Value;
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
            private string[] rawSplitLine;
            private string[] parsedLine;

            public ReadLine(string[] headers, Dictionary<string, int> headerLookup, int index, string raw, CsvOptions options)
            {
                this.headerLookup = headerLookup;
                this.options = options;
                Headers = headers;
                Raw = raw;
                Index = index;
            }

            public string[] Headers { get; }

            public string Raw { get; }

            public int Index { get; }

            public int ColumnCount => Line.Length;

            public bool HasColumn(string name) => headerLookup.TryGetValue(name, out _);

            internal string[] RawSplitLine
            {
                get
                {
                    if (rawSplitLine == null)
                    {
                        lock (headerLookup)
                        {
                            if (rawSplitLine == null)
                            {
                                rawSplitLine = SplitLine(Raw, options);
                            }
                        }
                    }
                    return rawSplitLine;
                }
            }

            public string[] Values => Line;

            private string[] Line
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
                        return Line[index];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw new InvalidOperationException($"Invalid row, missing {name} header, expected {Headers.Length} columns, got {Line.Length} columns.");
                    }
                }
            }

            string ICsvLine.this[int index] => Line[index];

            public override string ToString()
            {
                return Raw;
            }
        }
    }
}