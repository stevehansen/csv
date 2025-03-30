using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;
#else
using System; // NOTE: Used for Tuple
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
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
        private static readonly Dictionary<(char Separator, bool AllowSingleQuoteToEncloseFieldValues), CsvLineSplitter> splitterCache = new Dictionary<(char, bool), CsvLineSplitter>();
#else
        private static readonly Dictionary<Tuple<char, bool>, CsvLineSplitter> splitterCache = new Dictionary<Tuple<char, bool>, CsvLineSplitter>();
#endif

        private static readonly object syncRoot = new object();

        private readonly char separator;
        private readonly Regex splitter;
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
        private readonly bool useSpanSplitter;
#endif

        private CsvLineSplitter(char separator, Regex splitter)
        {
            this.separator = separator;
            this.splitter = splitter;
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
            // Default to using the span-based splitter for better performance
            this.useSpanSplitter = true;
#endif
        }

        public static CsvLineSplitter Get(CsvOptions options)
        {
            CsvLineSplitter? splitter;
            lock (syncRoot)
            {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
                var key = (options.Separator, options.AllowSingleQuoteToEncloseFieldValues);
#else
                var key = Tuple.Create(options.Separator, options.AllowSingleQuoteToEncloseFieldValues);
#endif
                if (!splitterCache.TryGetValue(key, out splitter))
                    splitterCache[key] = splitter = Create(options);
            }

            return splitter;
        }

        private static CsvLineSplitter Create(CsvOptions options)
        {
            const string patternEscape = @"(?>(?(IQ)(?(ESC).(?<-ESC>)|\\(?<ESC>))|(?!))|(?(IQ)\k<QUOTE>(?<-IQ>)|(?<=^|{0})(?<QUOTE>[{1}])(?<IQ>))|(?(IQ).|[^{0}]))+|^(?={0})|(?<={0})(?={0})|(?<={0})$";
            const string patternNoEscape = @"(?>(?(IQ)\k<QUOTE>(?<-IQ>)|(?<=^|{0})(?<QUOTE>[{1}])(?<IQ>))|(?(IQ).|[^{0}]))+|^(?={0})|(?<={0})(?={0})|(?<={0})$";
            var separator = Regex.Escape(options.Separator.ToString());
            var quoteChars = options.AllowSingleQuoteToEncloseFieldValues ? "\"'" : "\"";
            // Since netstandard1.0 doesn't include RegexOptions.Compiled, we include it by value (in case the target platform supports it)
            const RegexOptions regexOptions = RegexOptions.Singleline | ((RegexOptions/*.Compiled*/)8);
            if (options.AllowBackSlashToEscapeQuote)
                return new CsvLineSplitter(options.Separator, new Regex(string.Format(patternEscape, separator, quoteChars), regexOptions));
            return new CsvLineSplitter(options.Separator, new Regex(string.Format(patternNoEscape, separator, quoteChars), regexOptions));
        }

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

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
            // Optimized check for unterminated quoted value using spans directly
            if (IsUnterminatedQuotedValueFast(value, quoteChar, options.AllowBackSlashToEscapeQuote))
                return true;

            // Fall back to the regex-based check for complex cases
            var regex = options.AllowBackSlashToEscapeQuote ? $@"\\?{quoteChar}+$" : $@"{quoteChar}+$";
            var trailingQuotes = StringHelpers.RegexMatch(value.Slice(1), regex);

            // If the first trailing quote is escaped, ignore it
            if (options.AllowBackSlashToEscapeQuote && trailingQuotes.StartsWith('\\'))
            {
                trailingQuotes = trailingQuotes.AsSpan(2).ToString();
            }
            // the value is properly terminated if there are an odd number of unescaped quotes at the end
            return trailingQuotes.Length % 2 == 0;
#else
            var regex = options.AllowBackSlashToEscapeQuote ? $@"\\?{quoteChar}+$" : $@"{quoteChar}+$";
            var trailingQuotes = StringHelpers.RegexMatch(value.Substring(1), regex);
            // if the first trailing quote is escaped, ignore it
#if NET8_0_OR_GREATER
            if (options.AllowBackSlashToEscapeQuote && trailingQuotes.StartsWith('\\'))
#else
            if (options.AllowBackSlashToEscapeQuote && trailingQuotes.StartsWith("\\"))
#endif
            {
                trailingQuotes = trailingQuotes.Substring(2);
            }
            // the value is properly terminated if there are an odd number of unescaped quotes at the end
            return trailingQuotes.Length % 2 == 0;
#endif
        }

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
        /// <summary>
        /// Fast path for checking unterminated quoted values using spans directly
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsUnterminatedQuotedValueFast(ReadOnlySpan<char> value, char quoteChar, bool allowBackSlashEscape)
        {
            if (value.Length < 2 || value[0] != quoteChar)
                return false;

            // Count trailing quotes
            int quoteCount = 0;
            for (int i = value.Length - 1; i >= 1; i--)
            {
                if (value[i] != quoteChar)
                    break;

                // Handle escaped quotes
                if (allowBackSlashEscape && i > 0 && value[i - 1] == '\\')
                {
                    i--; // Skip the backslash
                    continue;
                }

                quoteCount++;
            }

            // If there are an even number of quotes, it's unterminated
            return quoteCount % 2 == 0;
        }

        /// <summary>
        /// Fast span-based CSV line splitting that avoids regex for better performance
        /// </summary>
        internal List<MemoryText> SplitLineSpan(MemoryText line, CsvOptions options)
        {
            var result = new List<MemoryText>();
            var span = line.Span;
            int start = 0;
            int position = 0;
            bool inQuotes = false;
            char currentQuoteChar = '\0';
            char separator = options.Separator;
            bool allowSingleQuote = options.AllowSingleQuoteToEncloseFieldValues;
            bool allowBackslashEscape = options.AllowBackSlashToEscapeQuote;

            // Fast path: if the line doesn't contain quotes, we can do a simple split
            bool hasQuotes = false;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == '"' || (allowSingleQuote && span[i] == '\''))
                {
                    hasQuotes = true;
                    break;
                }
            }

            if (!hasQuotes)
            {
                // Simple split by separator
                while (position < span.Length)
                {
                    if (span[position] == separator)
                    {
                        result.Add(line.Slice(start, position - start));
                        start = position + 1;
                    }
                    position++;
                }
                // Add the last field
                result.Add(line.Slice(start, position - start));
                return result;
            }

            // Complex case with quotes
            while (position < span.Length)
            {
                char c = span[position];

                // Handle quotes
                if ((c == '"' || (allowSingleQuote && c == '\'')) &&
                    (position == 0 || span[position - 1] != '\\' || !allowBackslashEscape))
                {
                    if (!inQuotes)
                    {
                        inQuotes = true;
                        currentQuoteChar = c;
                    }
                    else if (c == currentQuoteChar)
                    {
                        // Check for escaped quote
                        if (position + 1 < span.Length && span[position + 1] == currentQuoteChar)
                        {
                            // Skip the escaped quote
                            position++;
                        }
                        else
                        {
                            inQuotes = false;
                            currentQuoteChar = '\0';
                        }
                    }
                }
                // Handle separators (when not in quotes)
                else if (c == separator && !inQuotes)
                {
                    result.Add(line.Slice(start, position - start));
                    start = position + 1;
                }

                position++;
            }

            // Add the last field
            result.Add(line.Slice(start, position - start));
            return result;
        }
#endif

        public IList<MemoryText> Split(MemoryText line, CsvOptions options)
        {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
            // Use span-based splitting for better performance when possible
            if (useSpanSplitter && line.Length > 0)
            {
                try
                {
                    return SplitLineSpan(line, options);
                }
                catch
                {
                    // Fall back to regex splitter if any issues occur
                }
            }
#endif

            var matches = splitter.Matches(line.AsString());
            var values = new List<MemoryText>(matches.Count);
            var p = -1;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < matches.Count; i++)
            {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
                var value = line.Slice(matches[i].Index, matches[i].Length);
#else
                var value = matches[i].Value;
#endif
                if (p >= 0 && IsUnterminatedQuotedValue(values[p].AsSpan(), options))
                {
                    values[p] = StringHelpers.Concat(values[p], separator.ToString(), value);
                }
                else
                {
                    values.Add(value);
                    p++;
                }
            }
            return values;
        }
    }
}