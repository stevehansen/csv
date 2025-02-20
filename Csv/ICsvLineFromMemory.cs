#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1

using MemoryText = System.ReadOnlyMemory<char>;

namespace Csv
{
    /// <summary>
    /// Represents a single data line inside a csv file.
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
        /// Gets the 1-based index for the line inside the file.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets the number of columns of the line.
        /// </summary>
        int ColumnCount { get; }

        /// <summary>
        /// Indicates whether the specified <paramref name="name"/> exists.
        /// </summary>
        bool HasColumn(string name);

        /// <summary>
        /// Gets the data for the specified named header.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        MemoryText this[string name] { get; }

        /// <summary>
        /// Gets the data for the specified indexed header.
        /// </summary>
        /// <param name="index">The index of the header.</param>
        MemoryText this[int index] { get; }
    }
}

#endif