#if NET8_0_OR_GREATER

using MemoryText = System.ReadOnlyMemory<char>;

namespace Csv
{
    /// <summary>
    /// Represents a single record (data row) parsed from a csv file.
    /// </summary>
    public interface ICsvLineFromMemory
    {
        /// <summary>
        /// Gets the headers from the csv file.
        /// </summary>
        MemoryText[] Headers { get; }

        /// <summary>
        /// Gets a list of values in string format for the current row.
        /// </summary>
        MemoryText[] Values { get; }

        /// <summary>
        /// Gets the original raw content of the line.
        /// </summary>
        MemoryText Raw { get; }

        /// <summary>
        /// Gets the 1-based index of this record within the file.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets the number of fields in this record.
        /// </summary>
        int ColumnCount { get; }

        /// <summary>
        /// Indicates whether the specified <paramref name="name"/> exists.
        /// </summary>
        bool HasColumn(string name);

        /// <summary>
        /// Indicates whether the specified <paramref name="name"/> exists and
        /// this record contains a value for it.
        /// </summary>
        bool LineHasColumn(string name);

        /// <summary>
        /// Gets the data for the specified named header.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        MemoryText this[string name] { get; }

        /// <summary>
        /// Gets the value of the field at the specified index.
        /// </summary>
        /// <param name="index">The zero-based field index.</param>
        MemoryText this[int index] { get; }
    }
}

#endif