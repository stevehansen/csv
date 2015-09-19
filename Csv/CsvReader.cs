using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Csv
{
    /// <summary>
    /// Helper class to read csv (comma separated values) data.
    /// </summary>
    public static class CsvReader
    {
        public static IEnumerable<ICsvLine> Read(TextReader reader, CsvOptions options = null)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            return ReadImpl(reader, options);
        }

        public static IEnumerable<ICsvLine> ReadFromStream(Stream stream, CsvOptions options = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return ReadFromStreamImpl(stream, options);
        }

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
                if (index <= options.RowsToSkip)
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

                    headers = SplitLine(line, options);
                    headerLookup = headers.Select((h, idx) => Tuple.Create(h, idx)).ToDictionary(h => h.Item1, h => h.Item2);
                    continue;
                }

                yield return new ReadLine(headers, headerLookup, SplitLine(line, options));
            }
        }

        private static string[] SplitLine(string line, CsvOptions options)
        {
            return line.Split(options.Separator).Select(Unescape).ToArray();
        }

        private static string Unescape(string text)
        {
            text = text.Trim();

            if (text.StartsWith("\"") && text.EndsWith("\""))
                return text.Substring(1, text.Length - 2);

            return text;
        }

        private sealed class ReadLine : ICsvLine
        {
            private readonly Dictionary<string, int> headerLookup;
            private readonly string[] line;

            public ReadLine(string[] headers, Dictionary<string, int> headerLookup, string[] line)
            {
                this.headerLookup = headerLookup;
                this.line = line;
                Headers = headers;
            }

            public string[] Headers { get; }

            string ICsvLine.this[string header] => line[headerLookup[header]];

            string ICsvLine.this[int index] => line[index];

            public override string ToString()
            {
                return string.Join(";", line);
            }
        }
    }
}