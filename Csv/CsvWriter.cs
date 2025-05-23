﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
    }
}