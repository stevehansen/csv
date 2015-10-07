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
            while ((line = reader.ReadLine()) != null)
            {
                index++;
                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line, index) == true)
                    continue;

                if (headers == null)
                {
                    if (options.Separator == '\0')
                    {
                        // NOTE: Try simple 'detection' of possible separator
                        if (line.Contains(";"))
                            options.Separator = ';';
                        else if (line.Contains("\t"))
                            options.Separator = '\t';
                        else
                            options.Separator = ',';
                    }
                    options.Splitter = new Regex(@"(?>(?(IQ)(?(ESC).(?<-ESC>)|\\(?<ESC>))|(?!))|(?(IQ)\k<QUOTE>(?<-IQ>)|(?<QUOTE>[""'])(?<IQ>))|(?(IQ).|[^" + options.Separator + "]))+", (RegexOptions)8);

                    headers = SplitLine(line, options);
                    headerLookup = headers.Select((h, idx) => Tuple.Create(h, idx)).ToDictionary(h => h.Item1, h => h.Item2, options.Comparer);
                    continue;
                }

                yield return new ReadLine(headers, headerLookup, index, line, options);
            }
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

                if ((str.StartsWith("\"") && str.EndsWith("\"")) || (str.StartsWith("'") && str.EndsWith("'")))
                    str = str.Substring(1, str.Length - 2);

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

            private string[] Line
            {
                get
                {
                    if (parsedLine == null)
                    {
                        lock (options)
                        {
                            if (parsedLine == null)
                                parsedLine = SplitLine(Raw, options);
                        }
                    }

                    return parsedLine;
                }
            }

            string ICsvLine.this[string name]
            {
                get
                {
                    int index;
                    if (!headerLookup.TryGetValue(name, out index))
                        throw new ArgumentOutOfRangeException(nameof(name), name, $"Header '{name}' does not exist. Expected one of {string.Join("; ", headerLookup.Keys)}");

                    return Line[index];
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