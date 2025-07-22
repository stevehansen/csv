#if NET8_0_OR_GREATER

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Csv
{
    /// <summary>
    /// High-performance, memory-pool-based CSV writer that minimizes allocations.
    /// </summary>
    public sealed class CsvBufferWriter : IBufferWriter<char>, IDisposable
    {
        private readonly CsvMemoryOptions _options;
        private readonly List<(char[] buffer, int written, bool isPooled)> _buffers;
        private char[]? _currentBuffer;
        private int _currentPosition;
        private int _totalWritten;
        private bool _disposed;
        private bool _currentBufferIsPooled;

        /// <summary>
        /// Initializes a new instance of CsvBufferWriter with default options.
        /// </summary>
        public CsvBufferWriter() : this(new CsvMemoryOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of CsvBufferWriter with specified options.
        /// </summary>
        /// <param name="options">The memory options to use.</param>
        public CsvBufferWriter(CsvMemoryOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _buffers = new List<(char[] buffer, int written, bool isPooled)>();
        }

        /// <summary>
        /// Gets the total number of characters written.
        /// </summary>
        public int WrittenCount => _totalWritten;

        /// <summary>
        /// Gets the current capacity of the writer.
        /// </summary>
        public int Capacity => _buffers.Sum(b => b.buffer.Length) + (_currentBuffer?.Length ?? 0);

        /// <summary>
        /// Writes CSV data using ReadOnlyMemory for headers and rows.
        /// </summary>
        /// <param name="headers">The CSV headers.</param>
        /// <param name="rows">The CSV data rows.</param>
        /// <param name="separator">The column separator character.</param>
        /// <param name="skipHeaderRow">Whether to skip writing the header row.</param>
        public void WriteCsv(ReadOnlySpan<ReadOnlyMemory<char>> headers, IEnumerable<ReadOnlyMemory<char>[]> rows, char separator = ',', bool skipHeaderRow = false)
        {
            if (!skipHeaderRow && headers.Length > 0)
            {
                WriteRow(headers, separator);
            }

            foreach (var row in rows)
            {
                WriteRow(row.AsSpan(), separator);
            }
        }

        /// <summary>
        /// Writes a single CSV row.
        /// </summary>
        /// <param name="cells">The row cells.</param>
        /// <param name="separator">The column separator character.</param>
        public void WriteRow(ReadOnlySpan<ReadOnlyMemory<char>> cells, char separator = ',')
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                {
                    Write(separator);
                }

                WriteCell(cells[i].Span, separator);
            }

            Write(Environment.NewLine);
        }

        /// <summary>
        /// Writes a single cell with proper CSV escaping.
        /// </summary>
        /// <param name="cell">The cell content.</param>
        /// <param name="separator">The column separator character.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCell(ReadOnlySpan<char> cell, char separator = ',')
        {
            var needsQuoteEscape = cell.Contains('"');
            ReadOnlySpan<char> escapeChars = stackalloc char[] { separator, '\'', '\n', '\r' };
            var needsGeneralEscape = cell.IndexOfAny(escapeChars) >= 0;

            if (needsQuoteEscape || needsGeneralEscape)
            {
                Write('"');
                
                if (needsQuoteEscape)
                {
                    // Escape quotes by doubling them
                    for (int i = 0; i < cell.Length; i++)
                    {
                        var ch = cell[i];
                        Write(ch);
                        if (ch == '"')
                            Write('"');
                    }
                }
                else
                {
                    Write(cell);
                }
                
                Write('"');
            }
            else
            {
                Write(cell);
            }
        }

        /// <summary>
        /// Writes a single character.
        /// </summary>
        /// <param name="value">The character to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(char value)
        {
            var span = GetSpan(1);
            span[0] = value;
            Advance(1);
        }

        /// <summary>
        /// Writes a string.
        /// </summary>
        /// <param name="value">The string to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string value)
        {
            if (!string.IsNullOrEmpty(value))
                Write(value.AsSpan());
        }

        /// <summary>
        /// Writes a span of characters.
        /// </summary>
        /// <param name="value">The span to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySpan<char> value)
        {
            if (value.Length == 0) return;

            var span = GetSpan(value.Length);
            value.CopyTo(span);
            Advance(value.Length);
        }

        /// <summary>
        /// Gets a span to write to with at least the specified size.
        /// </summary>
        /// <param name="sizeHint">The minimum size required.</param>
        /// <returns>A writable span of characters.</returns>
        public Span<char> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(Math.Max(sizeHint, 1));
            return _currentBuffer.AsSpan(_currentPosition);
        }

        /// <summary>
        /// Gets memory to write to with at least the specified size.
        /// </summary>
        /// <param name="sizeHint">The minimum size required.</param>
        /// <returns>Writable memory.</returns>
        public Memory<char> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(Math.Max(sizeHint, 1));
            return _currentBuffer.AsMemory(_currentPosition);
        }

        /// <summary>
        /// Advances the position after writing the specified number of characters.
        /// </summary>
        /// <param name="count">The number of characters written.</param>
        public void Advance(int count)
        {
            if (count < 0 || _currentPosition + count > _currentBuffer?.Length)
                throw new ArgumentException("Invalid advance count", nameof(count));

            _currentPosition += count;
            _totalWritten += count;
        }

        /// <summary>
        /// Converts the written content to a string.
        /// </summary>
        /// <returns>The CSV content as a string.</returns>
        public override string ToString()
        {
            if (_totalWritten == 0)
                return string.Empty;

            var result = new char[_totalWritten];
            var resultSpan = result.AsSpan();
            var offset = 0;

            foreach (var (buffer, written, _) in _buffers)
            {
                if (offset >= _totalWritten) break;
                
                var length = Math.Min(written, _totalWritten - offset);
                buffer.AsSpan(0, length).CopyTo(resultSpan.Slice(offset, length));
                offset += length;
            }

            if (_currentBuffer != null && _currentPosition > 0 && offset < _totalWritten)
            {
                var length = Math.Min(_currentPosition, _totalWritten - offset);
                _currentBuffer.AsSpan(0, length).CopyTo(resultSpan.Slice(offset, length));
            }

            return new string(result);
        }

        /// <summary>
        /// Copies the written content to the specified span.
        /// </summary>
        /// <param name="destination">The destination span.</param>
        /// <returns>The number of characters copied.</returns>
        public int CopyTo(Span<char> destination)
        {
            if (_totalWritten == 0)
                return 0;

            var toCopy = Math.Min(_totalWritten, destination.Length);
            var copied = 0;

            foreach (var (buffer, written, _) in _buffers)
            {
                if (copied >= toCopy) break;

                var length = Math.Min(written, toCopy - copied);
                buffer.AsSpan(0, length).CopyTo(destination.Slice(copied));
                copied += length;
            }

            if (_currentBuffer != null && _currentPosition > 0 && copied < toCopy)
            {
                var length = Math.Min(_currentPosition, toCopy - copied);
                _currentBuffer.AsSpan(0, length).CopyTo(destination.Slice(copied));
                copied += length;
            }

            return copied;
        }

        /// <summary>
        /// Ensures the writer has capacity for at least the specified number of additional characters.
        /// </summary>
        /// <param name="capacity">The minimum additional capacity required.</param>
        private void EnsureCapacity(int capacity)
        {
            if (_currentBuffer != null && _currentPosition + capacity <= _currentBuffer.Length)
                return;

            // Move current buffer to completed buffers if it has content
            if (_currentBuffer != null && _currentPosition > 0)
            {
                // Trim the buffer to actual content size if it's significantly smaller and using pooled buffers
                if (_options.ReuseBuffers && _currentBufferIsPooled && _currentPosition < _currentBuffer.Length / 2)
                {
                    var trimmed = _options.CharArrayPool.Rent(_currentPosition);
                    // Only clear if security clearing is enabled and only the unused part
                    if (_options.ClearBuffers && trimmed.Length > _currentPosition)
                        Array.Clear(trimmed, _currentPosition, trimmed.Length - _currentPosition);
                    _currentBuffer.AsSpan(0, _currentPosition).CopyTo(trimmed);
                    _options.CharArrayPool.Return(_currentBuffer);
                    _currentBuffer = trimmed;
                }

                _buffers.Add((_currentBuffer, _currentPosition, _currentBufferIsPooled));
            }

            // Allocate new buffer - use direct allocation for small buffers to reduce memory overhead
            var newSize = Math.Max(capacity, _options.InitialBufferSize);
            newSize = Math.Min(newSize, _options.MaxBufferSize);
            
            if (newSize <= _options.DirectAllocationThreshold)
            {
                // Use direct allocation for small buffers to avoid ArrayPool overhead
                _currentBuffer = new char[newSize];
                _currentBufferIsPooled = false;
            }
            else
            {
                // Use ArrayPool for larger buffers
                _currentBuffer = _options.CharArrayPool.Rent(newSize);
                _currentBufferIsPooled = true;
                // Only clear if security clearing is enabled
                if (_options.ClearBuffers)
                    Array.Clear(_currentBuffer, 0, _currentBuffer.Length);
            }
            _currentPosition = 0;
        }

        /// <summary>
        /// Disposes the writer and returns buffers to the pool.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            foreach (var (buffer, _, isPooled) in _buffers)
            {
                if (isPooled)
                    _options.CharArrayPool.Return(buffer);
            }

            if (_currentBuffer != null)
            {
                if (_currentBufferIsPooled)
                    _options.CharArrayPool.Return(_currentBuffer);
                _currentBuffer = null;
            }

            _buffers.Clear();
            _disposed = true;
        }
    }
}

#endif