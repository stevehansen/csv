using System;

#if NET8_0_OR_GREATER
using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;

namespace Csv
{
    /// <summary>
    /// Enhanced CSV line interface with Span/Memory support for zero-allocation data access.
    /// </summary>
    public interface ICsvLineSpan : ICsvLine
    {
        /// <summary>
        /// Gets the headers as ReadOnlyMemory&lt;char&gt; for efficient memory access.
        /// </summary>
        ReadOnlyMemory<char>[] HeadersMemory { get; }

        /// <summary>
        /// Gets a list of values as ReadOnlyMemory&lt;char&gt; for the current row.
        /// </summary>
        ReadOnlyMemory<char>[] ValuesMemory { get; }

        /// <summary>
        /// Gets the original raw content of the line as ReadOnlyMemory&lt;char&gt;.
        /// </summary>
        ReadOnlyMemory<char> RawMemory { get; }

        /// <summary>
        /// Gets the headers as ReadOnlySpan&lt;char&gt; for zero-allocation access.
        /// </summary>
        ReadOnlySpan<char> HeadersSpan { get; }

        /// <summary>
        /// Gets the values as ReadOnlySpan&lt;char&gt; for zero-allocation access.
        /// </summary>
        ReadOnlySpan<char> ValuesSpan { get; }

        /// <summary>
        /// Gets the raw content as ReadOnlySpan&lt;char&gt; for zero-allocation access.
        /// </summary>
        ReadOnlySpan<char> RawSpan { get; }

        /// <summary>
        /// Gets the data for the specified named header as ReadOnlyMemory&lt;char&gt;.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        ReadOnlyMemory<char> GetMemory(string name);

        /// <summary>
        /// Gets the data for the specified column index as ReadOnlyMemory&lt;char&gt;.
        /// </summary>
        /// <param name="index">The zero-based index of the column.</param>
        ReadOnlyMemory<char> GetMemory(int index);

        /// <summary>
        /// Gets the data for the specified named header as ReadOnlySpan&lt;char&gt;.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        ReadOnlySpan<char> GetSpan(string name);

        /// <summary>
        /// Gets the data for the specified column index as ReadOnlySpan&lt;char&gt;.
        /// </summary>
        /// <param name="index">The zero-based index of the column.</param>
        ReadOnlySpan<char> GetSpan(int index);

        /// <summary>
        /// Tries to get the data for the specified named header as ReadOnlyMemory&lt;char&gt;.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">When this method returns, contains the memory for the column if found; otherwise, empty.</param>
        /// <returns>true if the column was found; otherwise, false.</returns>
        bool TryGetMemory(string name, out ReadOnlyMemory<char> value);

        /// <summary>
        /// Tries to get the data for the specified column index as ReadOnlyMemory&lt;char&gt;.
        /// </summary>
        /// <param name="index">The zero-based index of the column.</param>
        /// <param name="value">When this method returns, contains the memory for the column if found; otherwise, empty.</param>
        /// <returns>true if the column was found; otherwise, false.</returns>
        bool TryGetMemory(int index, out ReadOnlyMemory<char> value);

        /// <summary>
        /// Tries to get the data for the specified named header as ReadOnlySpan&lt;char&gt;.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">When this method returns, contains the span for the column if found; otherwise, empty.</param>
        /// <returns>true if the column was found; otherwise, false.</returns>
        bool TryGetSpan(string name, out ReadOnlySpan<char> value);

        /// <summary>
        /// Tries to get the data for the specified column index as ReadOnlySpan&lt;char&gt;.
        /// </summary>
        /// <param name="index">The zero-based index of the column.</param>
        /// <param name="value">When this method returns, contains the span for the column if found; otherwise, empty.</param>
        /// <returns>true if the column was found; otherwise, false.</returns>
        bool TryGetSpan(int index, out ReadOnlySpan<char> value);
    }
}

#endif