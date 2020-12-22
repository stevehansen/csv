using System;
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
#if NETCOREAPP3_1 || NETSTANDARD2_1

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

        internal static MemoryText Replace(this MemoryText str, string oldValue, string newValue)
        {
            return str.AsString().Replace(oldValue, newValue).AsMemory(); // TODO: Use Memory/Span
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
        public static string AsString(this MemoryText str)
        {
            return new string(str.Span);
        }

        internal static SpanText AsSpan(this MemoryText str)
        {
            return str.Span;
        }

        internal static string RegexMatch(SpanText str, string pattern)
        {
            return Regex.Match(new string(str), pattern).Value;
        }

        internal static MemoryText Concat(MemoryText str1, string str2, MemoryText str3)
        {
            return (str1.AsString() + str2 + str3.AsString()).AsMemory();
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

            return str[start..end];
        }
#endif
    }
#else
    internal static class StringHelpers
    {
        public static string RegexMatch(SpanText str, string pattern)
        {
            return Regex.Match(str, pattern).Value;
        }

        public static string AsString(this MemoryText str)
        {
            return str;
        }

        public static SpanText AsSpan(this MemoryText str)
        {
            return str;
        }

        public static MemoryText AsMemory(this string str)
        {
            return str;
        }

        public static MemoryText Concat(MemoryText str1, string str2, MemoryText str3)
        {
            return (str1 + str2 + str3);
        }
    }

#endif
}