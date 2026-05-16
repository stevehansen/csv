using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

#if NET8_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;
#else
using MemoryText = System.String;
using SpanText = System.String;
#endif

namespace Csv
{
    partial class CsvReader
    {
        internal interface ILineSource
        {
            bool TryReadLine(out MemoryText line, out string? lineString);
            MemoryText Concat(MemoryText head, string newLine, MemoryText tail, out string? combined);
        }

#if NET8_0_OR_GREATER
        internal interface IAsyncLineSource
        {
            ValueTask<(bool ok, MemoryText line, string? lineString)> TryReadLineAsync(CancellationToken ct);
            MemoryText Concat(MemoryText head, string newLine, MemoryText tail, out string? combined);
        }
#endif

        internal interface IRowFactory<TRow> where TRow : class
        {
            TRow Create(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, MemoryText raw, string? rawString, IList<MemoryText>? rawSplit, CsvOptions options);
        }

        internal readonly struct TextReaderLineSource : ILineSource
        {
            private readonly TextReader reader;

            public TextReaderLineSource(TextReader reader)
            {
                this.reader = reader;
            }

            public bool TryReadLine(out MemoryText line, out string? lineString)
            {
                var read = reader.ReadLine();
                if (read == null)
                {
                    line = default!;
                    lineString = null;
                    return false;
                }

                lineString = read;
#if NET8_0_OR_GREATER
                line = read.AsMemory();
#else
                line = read;
#endif
                return true;
            }

            public MemoryText Concat(MemoryText head, string newLine, MemoryText tail, out string? combined)
            {
#if NET8_0_OR_GREATER
                combined = string.Concat(head.Span, newLine.AsSpan(), tail.Span);
                return combined.AsMemory();
#else
                combined = head + newLine + tail;
                return combined;
#endif
            }
        }

#if NET8_0_OR_GREATER
        internal readonly struct AsyncTextReaderLineSource : IAsyncLineSource
        {
            private readonly TextReader reader;

            public AsyncTextReaderLineSource(TextReader reader)
            {
                this.reader = reader;
            }

            public async ValueTask<(bool ok, MemoryText line, string? lineString)> TryReadLineAsync(CancellationToken ct)
            {
                var read = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (read == null)
                    return (false, default, null);

                return (true, read.AsMemory(), read);
            }

            public MemoryText Concat(MemoryText head, string newLine, MemoryText tail, out string? combined)
            {
                combined = string.Concat(head.Span, newLine.AsSpan(), tail.Span);
                return combined.AsMemory();
            }
        }

        internal struct MemorySliceLineSource : ILineSource
        {
            private readonly ReadOnlyMemory<char> csv;
            private int position;

            public MemorySliceLineSource(ReadOnlyMemory<char> csv)
            {
                this.csv = csv;
                this.position = 0;
            }

            public bool TryReadLine(out MemoryText line, out string? lineString)
            {
                lineString = null;

                if (position >= csv.Length)
                {
                    line = default;
                    return false;
                }

                var span = csv.Span.Slice(position);
                var newlineIndex = span.IndexOfAny('\n', '\r');

                if (newlineIndex == -1)
                {
                    line = csv.Slice(position);
                    position = csv.Length;
                    return true;
                }

                line = csv.Slice(position, newlineIndex);
                position += newlineIndex;

                var ch = csv.Span[position];
                position++;
                if (position < csv.Length && ch == '\r' && csv.Span[position] == '\n')
                    position++;

                return true;
            }

            public MemoryText Concat(MemoryText head, string newLine, MemoryText tail, out string? combined)
            {
                combined = string.Concat(head.Span, newLine.AsSpan(), tail.Span);
                return combined.AsMemory();
            }
        }

        internal struct MemoryReaderLineSource : ILineSource
        {
            private readonly ReadOnlyMemory<char> csv;
            private int position;

            public MemoryReaderLineSource(ReadOnlyMemory<char> csv)
            {
                this.csv = csv;
                this.position = 0;
            }

            public bool TryReadLine(out MemoryText line, out string? lineString)
            {
                lineString = null;

                if (position >= csv.Length)
                {
                    line = default;
                    return false;
                }

                line = csv.ReadLine(ref position);
                return true;
            }

            public MemoryText Concat(MemoryText head, string newLine, MemoryText tail, out string? combined)
            {
                combined = null;
                return StringHelpers.Concat(head, newLine, tail);
            }
        }
#endif

        internal readonly struct StringRowFactory : IRowFactory<ReadLine>
        {
            public ReadLine Create(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, MemoryText raw, string? rawString, IList<MemoryText>? rawSplit, CsvOptions options)
            {
#if NET8_0_OR_GREATER
                var row = new ReadLine(headers, headerLookup, index, rawString ?? raw.ToString(), options);
#else
                var row = new ReadLine(headers, headerLookup, index, rawString ?? raw, options);
#endif
                if (rawSplit != null)
                    row.rawSplitLine = rawSplit;
                return row;
            }
        }

#if NET8_0_OR_GREATER
        internal readonly struct SpanRowFactory : IRowFactory<ReadLineSpan>
        {
            public ReadLineSpan Create(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, MemoryText raw, string? rawString, IList<MemoryText>? rawSplit, CsvOptions options)
            {
                var row = new ReadLineSpan(headers, headerLookup, index, rawString ?? raw.ToString(), options);
                if (rawSplit != null)
                    row.rawSplitLine = rawSplit;
                return row;
            }
        }

        internal readonly struct OptimizedRowFactory : IRowFactory<ReadLineSpanOptimized>
        {
            private readonly CsvMemoryOptions memoryOptions;

            public OptimizedRowFactory(CsvMemoryOptions memoryOptions)
            {
                this.memoryOptions = memoryOptions;
            }

            public ReadLineSpanOptimized Create(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, MemoryText raw, string? rawString, IList<MemoryText>? rawSplit, CsvOptions options)
            {
                var row = new ReadLineSpanOptimized(headers, headerLookup, index, raw, options, memoryOptions);
                if (rawSplit != null)
                    row.rawSplitLine = rawSplit;
                return row;
            }
        }

        internal readonly struct MemoryRowFactory : IRowFactory<ReadLineFromMemory>
        {
            public ReadLineFromMemory Create(MemoryText[] headers, Dictionary<string, int> headerLookup, int index, MemoryText raw, string? rawString, IList<MemoryText>? rawSplit, CsvOptions options)
            {
                var row = new ReadLineFromMemory(headers, headerLookup, index, raw, options);
                if (rawSplit != null)
                    row.rawSplitLine = rawSplit;
                return row;
            }
        }
#endif

        private static IEnumerable<TRow> Enumerate<TSource, TFactory, TRow>(TSource source, TFactory factory, CsvOptions options)
            where TSource : struct, ILineSource
            where TFactory : struct, IRowFactory<TRow>
            where TRow : class
        {
            Debug.Assert(options.Splitter == null, "CsvOptions cannot be reused across enumerations. Create a new instance.");

            var index = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;

            while (source.TryReadLine(out var line, out var lineString))
            {
                index++;

                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line, index) == true)
                    continue;

                IList<MemoryText>? rawSplit = null;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(line.AsSpan(), options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    // HeaderAbsent + multiline: complete the first data line before deriving column count,
                    // otherwise the headers are sized to a partial record. The yield loop below detects this
                    // case via index == RowsToSkip + 1 and skips its own multiline pass to avoid double-reading.
                    if (!skipInitialLine && options.AllowNewLineInEnclosedFieldValues)
                    {
                        rawSplit = options.Splitter.Split(line, options);

                        while (rawSplit.Count > 0 && CsvLineSplitter.IsUnterminatedQuotedValue(rawSplit[rawSplit.Count - 1].AsSpan(), options))
                        {
                            if (!source.TryReadLine(out var nextLine, out _))
                                break;

                            line = source.Concat(line, options.NewLine, nextLine, out lineString);
                            rawSplit = options.Splitter.Split(line, options);
                        }
                    }

                    headers = skipInitialLine ? GetHeaders(line, options) : CreateDefaultHeaders(line, options);

                    try
                    {
                        headerLookup = CreateHeaderLookup(headers, options);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException("Duplicate headers detected in HeaderPresent mode. If you don't have a header you can set the HeaderMode to HeaderAbsent.");
                    }

                    var aliases = options.Aliases;
                    if (aliases != null)
                    {
                        foreach (var aliasGroup in aliases)
                        {
                            var groupIndex = -1;
                            foreach (var alias in aliasGroup)
                            {
                                if (headerLookup.TryGetValue(alias, out var aliasIndex))
                                {
                                    if (groupIndex != -1)
                                        throw new InvalidOperationException("Found multiple matches within alias group: " + string.Join(";", aliasGroup));

                                    groupIndex = aliasIndex;
                                }
                            }

                            if (groupIndex != -1)
                            {
                                foreach (var alias in aliasGroup)
                                    headerLookup[alias] = groupIndex;
                            }
                        }
                    }

                    if (skipInitialLine)
                        continue;
                }

                var isFirstDataLineInHeaderAbsentMode = options.HeaderMode == HeaderMode.HeaderAbsent && index == (options.RowsToSkip + 1);
                if (options.AllowNewLineInEnclosedFieldValues && !isFirstDataLineInHeaderAbsentMode)
                {
                    rawSplit = options.Splitter.Split(line, options);
                    while (rawSplit.Count > 0 && CsvLineSplitter.IsUnterminatedQuotedValue(rawSplit[rawSplit.Count - 1].AsSpan(), options))
                    {
                        if (!source.TryReadLine(out var nextLine, out _))
                            break;

                        line = source.Concat(line, options.NewLine, nextLine, out lineString);
                        rawSplit = options.Splitter.Split(line, options);
                    }
                }

                yield return factory.Create(headers, headerLookup, index, line, lineString, rawSplit, options);
            }
        }

#if NET8_0_OR_GREATER
        private static async IAsyncEnumerable<TRow> EnumerateAsync<TSource, TFactory, TRow>(TSource source, TFactory factory, CsvOptions options, [EnumeratorCancellation] CancellationToken ct = default)
            where TSource : struct, IAsyncLineSource
            where TFactory : struct, IRowFactory<TRow>
            where TRow : class
        {
            Debug.Assert(options.Splitter == null, "CsvOptions cannot be reused across enumerations. Create a new instance.");

            var index = 0;
            MemoryText[]? headers = null;
            Dictionary<string, int>? headerLookup = null;

            while (true)
            {
                var (ok, line, lineString) = await source.TryReadLineAsync(ct).ConfigureAwait(false);
                if (!ok)
                    break;

                index++;

                if (index <= options.RowsToSkip || options.SkipRow?.Invoke(line, index) == true)
                    continue;

                IList<MemoryText>? rawSplit = null;

                if (headers == null || headerLookup == null)
                {
                    InitializeOptions(line.AsSpan(), options);
                    var skipInitialLine = options.HeaderMode == HeaderMode.HeaderPresent;

                    // HeaderAbsent + multiline: complete the first data line before deriving column count,
                    // otherwise the headers are sized to a partial record. The yield loop below detects this
                    // case via index == RowsToSkip + 1 and skips its own multiline pass to avoid double-reading.
                    if (!skipInitialLine && options.AllowNewLineInEnclosedFieldValues)
                    {
                        rawSplit = options.Splitter.Split(line, options);

                        while (rawSplit.Count > 0 && CsvLineSplitter.IsUnterminatedQuotedValue(rawSplit[rawSplit.Count - 1].AsSpan(), options))
                        {
                            var (nextOk, nextLine, _) = await source.TryReadLineAsync(ct).ConfigureAwait(false);
                            if (!nextOk)
                                break;

                            line = source.Concat(line, options.NewLine, nextLine, out lineString);
                            rawSplit = options.Splitter.Split(line, options);
                        }
                    }

                    headers = skipInitialLine ? GetHeaders(line, options) : CreateDefaultHeaders(line, options);

                    try
                    {
                        headerLookup = CreateHeaderLookup(headers, options);
                    }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException("Duplicate headers detected in HeaderPresent mode. If you don't have a header you can set the HeaderMode to HeaderAbsent.");
                    }

                    var aliases = options.Aliases;
                    if (aliases != null)
                    {
                        foreach (var aliasGroup in aliases)
                        {
                            var groupIndex = -1;
                            foreach (var alias in aliasGroup)
                            {
                                if (headerLookup.TryGetValue(alias, out var aliasIndex))
                                {
                                    if (groupIndex != -1)
                                        throw new InvalidOperationException("Found multiple matches within alias group: " + string.Join(";", aliasGroup));

                                    groupIndex = aliasIndex;
                                }
                            }

                            if (groupIndex != -1)
                            {
                                foreach (var alias in aliasGroup)
                                    headerLookup[alias] = groupIndex;
                            }
                        }
                    }

                    if (skipInitialLine)
                        continue;
                }

                var isFirstDataLineInHeaderAbsentMode = options.HeaderMode == HeaderMode.HeaderAbsent && index == (options.RowsToSkip + 1);
                if (options.AllowNewLineInEnclosedFieldValues && !isFirstDataLineInHeaderAbsentMode)
                {
                    rawSplit = options.Splitter.Split(line, options);
                    while (rawSplit.Count > 0 && CsvLineSplitter.IsUnterminatedQuotedValue(rawSplit[rawSplit.Count - 1].AsSpan(), options))
                    {
                        var (nextOk, nextLine, _) = await source.TryReadLineAsync(ct).ConfigureAwait(false);
                        if (!nextOk)
                            break;

                        line = source.Concat(line, options.NewLine, nextLine, out lineString);
                        rawSplit = options.Splitter.Split(line, options);
                    }
                }

                yield return factory.Create(headers, headerLookup, index, line, lineString, rawSplit, options);
            }
        }
#endif
    }
}
