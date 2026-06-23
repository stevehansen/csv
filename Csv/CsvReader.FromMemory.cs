#if NET8_0_OR_GREATER

using System.Collections.Generic;

using MemoryText = System.ReadOnlyMemory<char>;
using SpanText = System.ReadOnlySpan<char>;

namespace Csv
{
    partial class CsvReader
    {
        /// <summary>
        /// Reads the records from the csv string.
        /// </summary>
        /// <param name="csv">The csv string to read the data from.</param>
        /// <param name="options">The optional options to use when reading.</param>
        public static IEnumerable<ICsvLineFromMemory> ReadFromMemory(MemoryText csv, CsvOptions? options = null)
            => Enumerate<MemoryReaderLineSource, MemoryRowFactory, CsvLine<DefaultTrimSplit>>(new MemoryReaderLineSource(csv), default, options ?? new CsvOptions());
    }
}

#endif