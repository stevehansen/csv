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
    /// Splits a single record's text (multiline handling is done independently) into its fields
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
            if (value.Length == 0 || !options.AllowEnclosedFieldValues)
                return false;

            // When TrimData is set, the splitter accepts a quote opening after leading
            // whitespace (see Split below). Apply the same leniency here so multiline
            // detection agrees with the splitter — otherwise a field like ` "foo` would
            // be split as quoted but considered terminated, dropping the continuation.
            var start = 0;
            if (options.TrimData)
            {
                while (start < value.Length && IsWhitespace(value[start]))
                    start++;

                if (start >= value.Length)
                    return false;
            }

            char quoteChar;
            if (value[start] == '"')
            {
                quoteChar = '"';
            }
            else if (options.AllowSingleQuoteToEncloseFieldValues && value[start] == '\'')
            {
                quoteChar = '\'';
            }
            else
            {
                return false;
            }

#if NET8_0_OR_GREATER
            return IsUnterminatedQuotedValueCore(value[(start + 1)..], quoteChar, options.AllowBackSlashToEscapeQuote, options.AllowNewLineInEnclosedFieldValues);
#else
            return IsUnterminatedQuotedValueCore(value.Substring(start + 1), quoteChar, options.AllowBackSlashToEscapeQuote, options.AllowNewLineInEnclosedFieldValues);
#endif
        }

        private static bool IsWhitespace(char ch) => ch == ' ' || ch == '\t';

        // A quote may open a quoted field either at the literal field start or, when
        // TrimData is set, after any run of leading whitespace. This matches the
        // user-visible promise of TrimData: surrounding whitespace doesn't break the
        // structure of a quoted field (issue #71).
        private static bool IsAtFieldOpen(SpanText span, int start, int i, bool trimData)
        {
            if (i == start)
                return true;

            if (!trimData)
                return false;

            for (var k = start; k < i; k++)
            {
                if (!IsWhitespace(span[k]))
                    return false;
            }

            return true;
        }

        private static bool IsUnterminatedQuotedValueCore(SpanText value, char quoteChar, bool allowBackslashEscape, bool allowNewLineInEnclosedFieldValues)
        {
#if NET8_0_OR_GREATER
            var span = value;
#else
            var span = value;
#endif
            // Empty after removing opening quote means just a single quote - unterminated
            if (span.Length == 0)
                return true;

            // Check if there's any quote in the string
            int lastQuoteIndex = -1;
            for (int j = span.Length - 1; j >= 0; j--)
            {
                if (span[j] == quoteChar)
                {
                    lastQuoteIndex = j;
                    break;
                }
            }
            
            // No quotes at all means unterminated
            if (lastQuoteIndex == -1)
                return true;
                
            // If there's content after the last quote, it's considered terminated (though malformed)
            if (lastQuoteIndex < span.Length - 1)
                return false;
            
            // Count trailing quotes
            int trailingQuoteCount = 0;
            int i = span.Length - 1;
            
            while (i >= 0 && span[i] == quoteChar)
            {
                trailingQuoteCount++;
                i--;
            }
            
            // Check if the last quote is escaped by a backslash
            if (allowBackslashEscape && i >= 0 && span[i] == '\\')
            {
                // Count preceding backslashes
                int backslashCount = 0;
                while (i >= 0 && span[i] == '\\')
                {
                    backslashCount++;
                    i--;
                }
                
                // If odd number of backslashes, the last quote is escaped
                if (backslashCount % 2 == 1)
                {
                    // Remove the escaped quote from count
                    trailingQuoteCount--;
                    
                    // If no quotes left after removing the escaped one, it's unterminated
                    if (trailingQuoteCount == 0)
                        return true;
                }
            }
            
            // In CSV, quotes are escaped by doubling them ("" becomes ").
            // When checking if a quoted value is unterminated (continues on next line),
            // we need to determine if the trailing quotes indicate an incomplete field.
            //
            // According to RFC 4180:
            // - "" inside a quoted field = escaped quote (one " character in value)
            // - A single " after content = closing quote
            //
            // Therefore, for trailing quotes:
            // - 1 trailing quote = terminated (the closing quote)
            // - 2 trailing quotes = unterminated ("" is escaped quote, no closer)
            // - 3 trailing quotes = terminated ("" escaped + " closer, value ends with ")
            // - 4 trailing quotes = unterminated ("" + "" = two escaped, no closer)
            // - etc.
            //
            // Pattern: odd = terminated, even = unterminated

            if (trailingQuoteCount == 1)
                return false; // always terminated - this is the closing quote

            // Even number of trailing quotes means all are escaped pairs with no closer
            // Odd number means escape pairs + one closing quote
            return trailingQuoteCount % 2 == 0;
        }

        public IList<MemoryText> Split(MemoryText line, CsvOptions options, int? initialCapacity = null)
        {
#if NET8_0_OR_GREATER
            var span = line.Span;
#else
            var span = line;
#endif

            var values = initialCapacity.HasValue ? new List<MemoryText>(initialCapacity.Value) : new List<MemoryText>();
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
                    else if (options.AllowEnclosedFieldValues && (ch == '"' || (options.AllowSingleQuoteToEncloseFieldValues && ch == '\'')) && IsAtFieldOpen(span, start, i, options.TrimData))
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