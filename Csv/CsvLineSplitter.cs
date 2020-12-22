using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#if NETCOREAPP3_1 || NETSTANDARD2_1
using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;
#else
using MemoryText = System.String;
using SpanText = System.String;
#endif

namespace Csv
{
    internal sealed class CsvLineSplitter
    {
#if NETCOREAPP3_1 || NETSTANDARD2_1
        private static readonly Dictionary<(char Separator, bool AllowSingleQuoteToEncloseFieldValues), CsvLineSplitter> splitterCache = new Dictionary<(char, bool), CsvLineSplitter>();
#else
        private static readonly Dictionary<Tuple<char, bool>, CsvLineSplitter> splitterCache = new Dictionary<Tuple<char, bool>, CsvLineSplitter>();
#endif

        private static readonly object syncRoot = new object();

        private readonly Regex splitter;

        private CsvLineSplitter(Regex splitter)
        {
            this.splitter = splitter;
        }

        public static CsvLineSplitter Get(CsvOptions options)
        {
            CsvLineSplitter? splitter;
            lock (syncRoot)
            {
#if NETCOREAPP3_1 || NETSTANDARD2_1
                var key = (options.Separator, options.AllowSingleQuoteToEncloseFieldValues);
#else
                var key = Tuple.Create(options.Separator, options.AllowSingleQuoteToEncloseFieldValues);
#endif
                if (!splitterCache.TryGetValue(key, out splitter))
                    splitterCache[key] = splitter = CreateRegex(options);
            }

            return splitter;
        }

        private static CsvLineSplitter CreateRegex(CsvOptions options)
        {
            const string pattern = @"(?>(?(IQ)(?(ESC).(?<-ESC>)|\\(?<ESC>))|(?!))|(?(IQ)\k<QUOTE>(?<-IQ>)|(?<=^|{0})(?<QUOTE>[{1}])(?<IQ>))|(?(IQ).|[^{0}]))+|^(?={0})|(?<={0})(?={0})|(?<={0})$";
            var separator = Regex.Escape(options.Separator.ToString());
            var quoteChars = options.AllowSingleQuoteToEncloseFieldValues ? "\"'" : "\"";
            // Since netstandard1.0 doesn't include RegexOptions.Compiled, we include it by value (in case the target platform supports it)
            const RegexOptions regexOptions = RegexOptions.Singleline | ((RegexOptions/*.Compiled*/)8);
            return new CsvLineSplitter(new Regex(string.Format(pattern, separator, quoteChars), regexOptions));
        }

#if NETCOREAPP3_1 || NETSTANDARD2_1
        public MemoryText[] Matches(MemoryText line)
        {
            var matches = splitter.Matches(line.AsString());
            var values = new ReadOnlyMemory<char>[matches.Count];
            for (var i = 0; i < matches.Count; i++)
                values[i] = line.Slice(matches[i].Index, matches[i].Length);
            return values;
        }
        
        public int MatchesCount(SpanText line)
        {
            return splitter.Matches(new string(line)).Count;
        }
#else
        public MemoryText[] Matches(MemoryText line)
        {
            var matches = splitter.Matches(line);
            var values = new string[matches.Count];
            for (var i = 0; i < matches.Count; i++)
                values[i] = matches[i].Value;
            return values;
        }

        public int MatchesCount(SpanText line)
        {
            return splitter.Matches(line).Count;
        }
#endif
    }
}