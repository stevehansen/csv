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
            if (value.Length == 0 || !options.AllowEnclosedFieldValues)
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

#if NET8_0_OR_GREATER
            return IsUnterminatedQuotedValueCore(value[1..], quoteChar, options.AllowBackSlashToEscapeQuote, options.AllowNewLineInEnclosedFieldValues);
#else
            return IsUnterminatedQuotedValueCore(value.Substring(1), quoteChar, options.AllowBackSlashToEscapeQuote, options.AllowNewLineInEnclosedFieldValues);
#endif
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
                    else if (options.AllowEnclosedFieldValues && (ch == '"' || (options.AllowSingleQuoteToEncloseFieldValues && ch == '\'')) && i == start)
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