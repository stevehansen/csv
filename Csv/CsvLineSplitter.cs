using System.Collections.Generic;

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
    /// Splits a single line (multiline handling is done independently) into multiple parts
    /// </summary>
    internal sealed class CsvLineSplitter
    {
        private readonly char separator;

        private CsvLineSplitter(char separator)
        {
            this.separator = separator;
        }

        public static CsvLineSplitter Get(CsvOptions options) => new CsvLineSplitter(options.Separator);

        public static bool IsUnterminatedQuotedValue(SpanText value, CsvOptions options)
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

            var regex = options.AllowBackSlashToEscapeQuote ? $@"\\?{quoteChar}+$" : $@"{quoteChar}+$";
#if NET8_0_OR_GREATER
            var trailingQuotes = StringHelpers.RegexMatch(value[1..], regex);
#else
            var trailingQuotes = StringHelpers.RegexMatch(value.Substring(1), regex);
#endif
            // if the first trailing quote is escaped, ignore it
#if NET8_0_OR_GREATER
            if (options.AllowBackSlashToEscapeQuote && trailingQuotes.StartsWith('\\'))
#else
            if (options.AllowBackSlashToEscapeQuote && trailingQuotes.StartsWith("\\"))
#endif
            {
#if NET8_0_OR_GREATER
                trailingQuotes = trailingQuotes[2..];
#else
                trailingQuotes = trailingQuotes.Substring(2);
#endif
            }
            // the value is properly terminated if there are an odd number of unescaped quotes at the end
            return trailingQuotes.Length % 2 == 0;
        }

        public IList<MemoryText> Split(MemoryText line, CsvOptions options)
        {
#if NET8_0_OR_GREATER
            var span = line.Span;
#else
            var span = line;
#endif

            var values = new List<MemoryText>();
            var start = 0;
            var inQuotes = false;
            char quoteChar = '\0';

            for (var i = 0; i < span.Length; i++)
            {
                var ch = span[i];
                if (inQuotes)
                {
                    if (options.AllowBackSlashToEscapeQuote && ch == '\\' && i + 1 < span.Length && span[i + 1] == quoteChar)
                    {
                        i++; // skip escaped quote
                    }
                    else if (ch == quoteChar)
                    {
                        if (i + 1 < span.Length && span[i + 1] == quoteChar)
                        {
                            i++; // escaped quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                }
                else
                {
                    if (ch == separator)
                    {
#if NET8_0_OR_GREATER
                        var value = line.Slice(start, i - start);
#else
                        var value = line.Substring(start, i - start);
#endif
                        values.Add(value);
                        start = i + 1;
                    }
                    else if ((ch == '"' || (options.AllowSingleQuoteToEncloseFieldValues && ch == '\'')) && i == start)
                    {
                        inQuotes = true;
                        quoteChar = ch;
                    }
                }
            }

#if NET8_0_OR_GREATER
            var last = line.Slice(start);
#else
            var last = line.Substring(start);
#endif
            values.Add(last);
            return values;
        }
    }
}