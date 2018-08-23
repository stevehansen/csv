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
        private static readonly Dictionary<char, Regex> splitterCache = new Dictionary<char, Regex>();
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

                    headerLookup = headers.Select((h, idx) => Tuple.Create(h, idx)).ToDictionary(h => h.Item1, h => h.Item2, options.Comparer);

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

                // TODO: #11 might need to read another line if this one isn't ended yet (i.e. open quoted value)

                yield return new ReadLine(headers, headerLookup, index, line, options);
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
            return SplitLine(line, options);
        }

        private static void InitializeOptions(string line, CsvOptions options)
        {
            if (options.Separator == '\0')
                options.Separator = AutoDetectSeparator(line);


            Regex splitter;
            lock (syncRoot)
            {
                if (!splitterCache.TryGetValue(options.Separator, out splitter))
                    splitterCache[options.Separator] = splitter = new Regex(string.Format(@"(?>(?(IQ)(?(ESC).(?<-ESC>)|\\(?<ESC>))|(?!))|(?(IQ)\k<QUOTE>(?<-IQ>)|(?<QUOTE>"")(?<IQ>))|(?(IQ).|[^{0}]))+|^(?={0})|(?<={0})(?={0})|(?<={0})$", Regex.Escape(options.Separator.ToString())), (RegexOptions)8);
            }

            options.Splitter = splitter;
        }

        private static string[] SplitLine(string line, CsvOptions options)
        {
            var matches = options.Splitter.Matches(line);
            var parts = new string[matches.Count];
            for (var i = 0; i < matches.Count; i++)
            {
                var str = matches[i].Value;
                if (options.TrimData)
                    str = str.Trim();

                if (str.StartsWith("\"") && str.EndsWith("\""))
                    str = str.Substring(1, str.Length - 2).Replace("\"\"", "\"");

                parts[i] = str;
            }
            return parts;
        }

        private sealed class ReadLine : ICsvLine
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly CsvOptions options;
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

            private string[] Line
            {
                get
                {
                    if (parsedLine == null)
                    {
                        lock (headerLookup)
                        {
                            if (parsedLine == null)
                                parsedLine = SplitLine(Raw, options);

                            if (options.ValidateColumnCount && parsedLine.Length != Headers.Length)
                                throw new InvalidOperationException($"Expected {Headers.Length}, got {parsedLine.Length} columns.");
                        }
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