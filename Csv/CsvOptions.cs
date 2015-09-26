using System;
using System.Collections.Generic;

namespace Csv
{
    /// <summary>
    /// Defines the options that can be passed to customize the reading or writing of csv files.
    /// </summary>
    public sealed class CsvOptions
    {
        /// <summary>
        /// Gets or sets the number of rows to skip before reading the header row, defaults to <c>0</c>.
        /// </summary>
        public int RowsToSkip { get; set; }

        /// <summary>
        /// Gets or sets a function to skip the current row based on its raw string value or 1-based index. Skips empty rows and rows starting with # by default.
        /// </summary>
        public Func<string, int, bool> SkipRow { get; set; } = (row, idx) => string.IsNullOrEmpty(row) || row[0] == '#';

        /// <summary>
        ///  Gets or sets the character to use for separating data, defaults to <c>'\0'</c> which will auto-detect from the header row.
        /// </summary>
        public char Separator { get; set; }

        /// <summary>
        /// Gets or sets wether data should be trimmed when accessed.
        /// </summary>
        public bool TrimData { get; set; }

        /// <summary>
        /// Gets or sets the comparer to use when looking up header names.
        /// </summary>
        public IEqualityComparer<string> Comparer { get; set; }
    }
}