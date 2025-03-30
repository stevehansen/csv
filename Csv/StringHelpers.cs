using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;
#else
using MemoryText = System.String;
using SpanText = System.String;
#endif

[assembly: InternalsVisibleTo("Csv.Tests")]

namespace Csv
{
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1

    /// <summary>
    /// Extension methods for <see cref="ReadOnlyMemory{Char}"/> to handle common string operations.
    /// </summary>
    public static class StringHelpers
    {
        /// <summary>
        /// Checks whether <paramref name="str" /> starts with <paramref name="value"/>.
        /// </summary>
        public static bool StartsWith(this MemoryText str, string value)
        {
            if (str.Length < value.Length)
                return false;

            var span = str.Span;
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < value.Length; i++)
            {
                if (span[i] != value[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether <paramref name="str" /> ends with <paramref name="value"/>.
        /// </summary>
        public static bool EndsWith(this MemoryText str, string value)
        {
            var valueLength = value.Length;
            if (str.Length < valueLength)
                return false;

            var span = str.Span;
            for (var i = 0; i < valueLength; i++)
            {
                var spanIndex = valueLength - i;
                if (span[^spanIndex] != value[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Fast implementation for unescaping characters using Span
        /// </summary>
        internal static MemoryText Unescape(this MemoryText str, char escape, char actual, int start = 0)
        {
            // We assume that most values will have none or one escaped sequence, so optimize for that
            var span = str.Span;
            var maxLength = span.Length - 1;

            // Fast path: scan for escape character
            bool hasEscape = false;
            for (var i = start; i < maxLength; i++)
            {
                if (span[i] == escape && span[i + 1] == actual)
                {
                    hasEscape = true;
                    break;
                }
            }

            // If no escape sequences, return original
            if (!hasEscape)
                return str;

            // Process escapes
            for (var i = start; i < maxLength; i++)
            {
                if (span[i] == escape && span[i + 1] == actual)
                {
                    // ie: "test#-test", '#', '-' would return "test-test"
                    // i would be 4 and we need to keep the first 4, skip 1, and keep the rest
                    var actualStart = i + 1;
                    var remainder = str[actualStart..].Unescape(escape, actual, 1); // Skip the first char as it is already unescaped
                    var chars = new char[i + remainder.Length];
                    var result = new Memory<char>(chars);
                    str[..i].CopyTo(result);
                    remainder.CopyTo(result[i..]);
                    return result;
                }
            }

            return str;
        }

        /// <summary>
        /// Result structure for span-based line reading since ref structs can't be used in tuples
        /// </summary>
        public ref struct LineSpanResult
        {
            public ReadOnlySpan<char> Line;
            public int Position;
        }

        /// <summary>
        /// Optimized ReadLine method that works directly with ReadOnlySpan
        /// </summary>
        public static LineSpanResult ReadLineSpanFast(this ReadOnlySpan<char> data, int startPosition)
        {
            int position = startPosition;
            int lineStart = position;

            // Fast path for empty data
            if (position >= data.Length)
            {
                return new LineSpanResult { Line = ReadOnlySpan<char>.Empty, Position = position };
            }

            // SIMD-friendly loop: scan for newline characters
            while (position < data.Length)
            {
                char c = data[position];

                if (c == '\n')
                {
                    // Handle CRLF
                    int length = position - lineStart;
                    if (position > lineStart && data[position - 1] == '\r')
                    {
                        length--;
                    }

                    position++; // Skip past the newline
                    return new LineSpanResult { Line = data.Slice(lineStart, length), Position = position };
                }

                position++;
            }

            // Handle last line without newline
            if (position > lineStart)
            {
                return new LineSpanResult { Line = data.Slice(lineStart, position - lineStart), Position = position };
            }

            return new LineSpanResult { Line = ReadOnlySpan<char>.Empty, Position = position };
        }

        internal static MemoryText ReadLine(this MemoryText reader, ref int position)
        {
            var begin = position;
            var end = position;

            var span = reader.Span;
            for (; position < reader.Length; position++, end = position)
            {
                if (span[position] == '\n')
                {
                    if (position > 0 && span[position - 1] == '\r')
                        end = position - 1;

                    position++;

                    break;
                }
            }

            return reader[begin..end];
        }

        /// <summary>
        /// Gets a <see cref="string"/> value for <paramref name="str"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string AsString(this MemoryText str)
        {
            return str.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SpanText AsSpan(this MemoryText str)
        {
            return str.Span;
        }

        internal static string RegexMatch(SpanText str, string pattern)
        {
            return Regex.Match(str.ToString(), pattern).Value;
        }

        /// <summary>
        /// Optimized trim that works directly on spans
        /// </summary>
        internal static ReadOnlySpan<char> TrimSpan(this ReadOnlySpan<char> span)
        {
            int start = 0;
            int end = span.Length - 1;

            // Find the first non-whitespace character
            while (start <= end && char.IsWhiteSpace(span[start]))
            {
                start++;
            }

            // Find the last non-whitespace character
            while (end >= start && char.IsWhiteSpace(span[end]))
            {
                end--;
            }

            // If no non-whitespace characters, return empty
            if (start > end)
                return ReadOnlySpan<char>.Empty;

            return span.Slice(start, end - start + 1);
        }

#if NETSTANDARD2_1
        internal static ReadOnlyMemory<char> Trim(this ReadOnlyMemory<char> str)
        {
            var span = str.Span;
            var start = 0;
            var end = str.Length - 1;
            for (; start < str.Length; start++)
            {
                if (!char.IsWhiteSpace(span[start]))
                    break;
            }

            for (; end >= start; end--)
            {
                if (!char.IsWhiteSpace(span[end]))
                    break;
            }

            return str[start..(end + 1)];
        }

        internal static MemoryText Concat(MemoryText str1, string str2, MemoryText str3)
        {
            return (str1.AsString() + str2 + str3.AsString()).AsMemory();
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MemoryText Concat(MemoryText str1, string str2, MemoryText str3)
        {
            return string.Concat(str1.Span, str2.AsSpan(), str3.Span).AsMemory();
        }
#endif
    }
#else
    internal static class StringHelpers // NOTE: Extension methods are provided to reuse the same code
    {
        [MethodImpl((MethodImplOptions)256 /*MethodImplOptions.AggressiveInlining*/)]
        public static string RegexMatch(SpanText str, string pattern)
        {
            return Regex.Match(str, pattern).Value;
        }

        [MethodImpl((MethodImplOptions)256 /*MethodImplOptions.AggressiveInlining*/)]
        public static string AsString(this MemoryText str)
        {
            return str;
        }

        [MethodImpl((MethodImplOptions)256 /*MethodImplOptions.AggressiveInlining*/)]
        public static SpanText AsSpan(this MemoryText str)
        {
            return str;
        }

        [MethodImpl((MethodImplOptions)256 /*MethodImplOptions.AggressiveInlining*/)]
        public static MemoryText AsMemory(this string str)
        {
            return str;
        }

        [MethodImpl((MethodImplOptions)256 /*MethodImplOptions.AggressiveInlining*/)]
        public static MemoryText Concat(MemoryText str1, string str2, MemoryText str3)
        {
            return (str1 + str2 + str3);
        }
    }

#endif
}