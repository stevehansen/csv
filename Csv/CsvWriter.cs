using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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
    /// Helper class to write csv (comma separated values) data.
    /// </summary>
    public static class CsvWriter
    {
        /// <summary>
        /// Writes the lines to the writer.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static void Write(TextWriter writer, string[] headers, IEnumerable<string[]> lines, char separator = ',', bool skipHeaderRow = false)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            var columnCount = headers.Length;
            if (!skipHeaderRow)
                WriteLine(writer, headers, columnCount, separator);
            foreach (var line in lines)
                WriteLine(writer, line, columnCount, separator);
        }

        /// <summary>
        /// Writes the lines and return the result.
        /// </summary>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static string WriteToText(string[] headers, IEnumerable<string[]> lines, char separator = ',', bool skipHeaderRow = false)
        {
            using (var writer = new StringWriter())
            {
                Write(writer, headers, lines, separator, skipHeaderRow);

                return writer.ToString();
            }
        }

#if NET8_0_OR_GREATER

        /// <summary>
        /// Writes the lines to the writer using memory-efficient ReadOnlyMemory&lt;char&gt; arrays.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static void Write(TextWriter writer, ReadOnlyMemory<char>[] headers, IEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',', bool skipHeaderRow = false)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            var columnCount = headers.Length;
            if (!skipHeaderRow)
                WriteLineMemory(writer, headers, columnCount, separator);
            foreach (var line in lines)
                WriteLineMemory(writer, line, columnCount, separator);
        }

        /// <summary>
        /// Writes the lines to the writer using memory-efficient ReadOnlySpan&lt;char&gt; headers.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static void Write(TextWriter writer, ReadOnlySpan<ReadOnlyMemory<char>> headers, IEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',', bool skipHeaderRow = false)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            var columnCount = headers.Length;
            if (!skipHeaderRow)
                WriteLineMemorySpan(writer, headers, columnCount, separator);
            foreach (var line in lines)
                WriteLineMemory(writer, line, columnCount, separator);
        }

        /// <summary>
        /// Writes the lines and return the result using memory-efficient operations.
        /// </summary>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static string WriteToText(ReadOnlyMemory<char>[] headers, IEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',', bool skipHeaderRow = false)
        {
            using (var writer = new StringWriter())
            {
                Write(writer, headers, lines, separator, skipHeaderRow);
                return writer.ToString();
            }
        }

        /// <summary>
        /// High-performance buffer-based writing with pre-allocated destination buffer.
        /// </summary>
        /// <param name="buffer">The destination buffer to write to.</param>
        /// <param name="headers">The headers for the CSV.</param>
        /// <param name="lines">The data lines to write.</param>
        /// <param name="separator">The separator to use between columns.</param>
        /// <param name="written">The number of characters written to the buffer.</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        /// <returns>True if all data was written successfully, false if buffer was too small.</returns>
        public static bool WriteToBuffer(Span<char> buffer, ReadOnlySpan<ReadOnlyMemory<char>> headers, ReadOnlySpan<ReadOnlyMemory<char>[]> lines, char separator, out int written, bool skipHeaderRow = false)
        {
            written = 0;
            var pos = 0;

            if (!skipHeaderRow && headers.Length > 0)
            {
                if (!WriteLineToBuffer(buffer.Slice(pos), headers, separator, out var lineWritten))
                    return false;
                pos += lineWritten;
            }

            foreach (var line in lines)
            {
                if (pos >= buffer.Length)
                    return false;
                
                if (!WriteLineToBuffer(buffer.Slice(pos), line.AsSpan(), separator, out var lineWritten))
                    return false;
                pos += lineWritten;
            }

            written = pos;
            return true;
        }

#endif

#if NET8_0_OR_GREATER

        /// <summary>
        /// Writes the lines to the writer.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static async Task WriteAsync(TextWriter writer, string[] headers, IAsyncEnumerable<string[]> lines, char separator = ',', bool skipHeaderRow = false) =>
            await WriteAsync(writer, headers, lines, separator, skipHeaderRow, CancellationToken.None);

        /// <summary>
        /// Writes the lines to the writer.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        /// <param name="cancellationToken">The cancellation token to use.</param>
        public static async Task WriteAsync(TextWriter writer, string[] headers, IAsyncEnumerable<string[]> lines, char separator = ',', bool skipHeaderRow = false,
            CancellationToken cancellationToken = default)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            var columnCount = headers.Length;
            if (!skipHeaderRow)
                WriteLine(writer, headers, columnCount, separator);
            if (cancellationToken.CanBeCanceled)
            {
                await foreach (var line in lines.WithCancellation(cancellationToken))
                {
                    WriteLine(writer, line, columnCount, separator);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            else // no cancellation token, use fast iteration
            {
                await foreach (var line in lines)
                    WriteLine(writer, line, columnCount, separator);
            }
        }

        /// <summary>
        /// Writes the lines and return the result.
        /// </summary>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static async Task<string> WriteToTextAsync(string[] headers, IAsyncEnumerable<string[]> lines, char separator = ',', bool skipHeaderRow = false)
        {
            await using var writer = new StringWriter();
            await WriteAsync(writer, headers, lines, separator, skipHeaderRow);
            return writer.ToString();
        }

        /// <summary>
        /// Asynchronously writes the lines to the writer using memory-efficient ReadOnlyMemory&lt;char&gt; arrays.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static async Task WriteAsync(TextWriter writer, ReadOnlyMemory<char>[] headers, IAsyncEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',', bool skipHeaderRow = false) =>
            await WriteAsync(writer, headers, lines, separator, skipHeaderRow, CancellationToken.None);

        /// <summary>
        /// Asynchronously writes the lines to the writer using memory-efficient ReadOnlyMemory&lt;char&gt; arrays.
        /// </summary>
        /// <param name="writer">The text writer to write the data to.</param>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        /// <param name="cancellationToken">The cancellation token to use.</param>
        public static async Task WriteAsync(TextWriter writer, ReadOnlyMemory<char>[] headers, IAsyncEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',', bool skipHeaderRow = false,
            CancellationToken cancellationToken = default)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            var columnCount = headers.Length;
            if (!skipHeaderRow)
                WriteLineMemory(writer, headers, columnCount, separator);
            
            if (cancellationToken.CanBeCanceled)
            {
                await foreach (var line in lines.WithCancellation(cancellationToken))
                {
                    WriteLineMemory(writer, line, columnCount, separator);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            else
            {
                await foreach (var line in lines)
                    WriteLineMemory(writer, line, columnCount, separator);
            }
        }

        /// <summary>
        /// Asynchronously writes the lines and return the result using memory-efficient operations.
        /// </summary>
        /// <param name="headers">The headers that should be used for the first line, determines the number of columns.</param>
        /// <param name="lines">The lines with data that should be written.</param>
        /// <param name="separator">The separator to use between columns (comma, semicolon, tab, ...)</param>
        /// <param name="skipHeaderRow">Indicate whether the header row should be skipped, defaults to <c>false</c>.</param>
        public static async Task<string> WriteToTextAsync(ReadOnlyMemory<char>[] headers, IAsyncEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',', bool skipHeaderRow = false)
        {
            await using var writer = new StringWriter();
            await WriteAsync(writer, headers, lines, separator, skipHeaderRow);
            return writer.ToString();
        }

#endif

        private static void WriteLine(TextWriter writer, string[] data, int columnCount, char separator)
        {
            var escapeChars = new[] { separator, '\'', '\n' };
            for (var i = 0; i < columnCount; i++)
            {
                if (i > 0)
                    writer.Write(separator);

                if (i < data.Length)
                {
                    var escape = false;
                    var cell = data[i] ?? string.Empty;
#if NET8_0_OR_GREATER
                    if (cell.Contains('"'))
#else
                    if (cell.Contains("\""))
#endif
                    {
                        escape = true;
                        cell = cell.Replace("\"", "\"\"");
                    }
                    else if (cell.IndexOfAny(escapeChars) >= 0)
                        escape = true;

                    if (escape)
                        writer.Write('"');
                    writer.Write(cell);
                    if (escape)
                        writer.Write('"');
                }
            }

            writer.WriteLine();
        }

#if NET8_0_OR_GREATER

        private static void WriteLineMemory(TextWriter writer, ReadOnlyMemory<char>[] data, int columnCount, char separator)
        {
            var escapeChars = new[] { separator, '\'', '\n' };
            for (var i = 0; i < columnCount; i++)
            {
                if (i > 0)
                    writer.Write(separator);

                if (i < data.Length)
                {
                    var cell = data[i];
                    WriteCellMemory(writer, cell.Span, separator, escapeChars);
                }
            }

            writer.WriteLine();
        }

        private static void WriteLineMemorySpan(TextWriter writer, ReadOnlySpan<ReadOnlyMemory<char>> data, int columnCount, char separator)
        {
            var escapeChars = new[] { separator, '\'', '\n' };
            for (var i = 0; i < columnCount; i++)
            {
                if (i > 0)
                    writer.Write(separator);

                if (i < data.Length)
                {
                    var cell = data[i];
                    WriteCellMemory(writer, cell.Span, separator, escapeChars);
                }
            }

            writer.WriteLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteCellMemory(TextWriter writer, ReadOnlySpan<char> cell, char separator, char[] escapeChars)
        {
            var escape = false;
            
            // Check if we need to escape
            if (cell.Contains('"'))
            {
                escape = true;
                // Need to escape quotes by doubling them
                if (escape)
                    writer.Write('"');
                
                for (int i = 0; i < cell.Length; i++)
                {
                    var ch = cell[i];
                    if (ch == '"')
                        writer.Write("\"\"");
                    else
                        writer.Write(ch);
                }
                
                if (escape)
                    writer.Write('"');
            }
            else if (cell.IndexOfAny(escapeChars) >= 0)
            {
                escape = true;
                writer.Write('"');
                writer.Write(cell);
                writer.Write('"');
            }
            else
            {
                writer.Write(cell);
            }
        }

        private static bool WriteLineToBuffer(Span<char> buffer, ReadOnlySpan<ReadOnlyMemory<char>> data, char separator, out int written)
        {
            written = 0;
            var pos = 0;

            for (var i = 0; i < data.Length; i++)
            {
                if (i > 0)
                {
                    if (pos >= buffer.Length)
                        return false;
                    buffer[pos++] = separator;
                }

                var cell = data[i].Span;
                if (!WriteCellToBuffer(buffer.Slice(pos), cell, separator, out var cellWritten))
                    return false;
                pos += cellWritten;
            }

            // Add newline
            if (pos >= buffer.Length)
                return false;
            buffer[pos++] = '\n';

            written = pos;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool WriteCellToBuffer(Span<char> buffer, ReadOnlySpan<char> cell, char separator, out int written)
        {
            written = 0;
            var pos = 0;
            var escape = false;
            var escapeChars = new[] { separator, '\'', '\n' };

            // Check if we need to escape
            var needsQuoteEscape = cell.Contains('"');
            var needsGeneralEscape = cell.IndexOfAny(escapeChars) >= 0;
            escape = needsQuoteEscape || needsGeneralEscape;

            if (escape)
            {
                if (pos >= buffer.Length) return false;
                buffer[pos++] = '"';
            }

            if (needsQuoteEscape)
            {
                // Need to escape quotes by doubling them
                for (int i = 0; i < cell.Length; i++)
                {
                    var ch = cell[i];
                    if (ch == '"')
                    {
                        if (pos + 1 >= buffer.Length) return false;
                        buffer[pos++] = '"';
                        buffer[pos++] = '"';
                    }
                    else
                    {
                        if (pos >= buffer.Length) return false;
                        buffer[pos++] = ch;
                    }
                }
            }
            else
            {
                if (pos + cell.Length > buffer.Length) return false;
                cell.CopyTo(buffer.Slice(pos));
                pos += cell.Length;
            }

            if (escape)
            {
                if (pos >= buffer.Length) return false;
                buffer[pos++] = '"';
            }

            written = pos;
            return true;
        }

#endif
    }
}