using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Csv
{
    /// <summary>
    /// Defines the options that can be passed to customize the reading or writing of csv files.
    /// </summary>
    /// <remarks>
    /// Do not reuse an instance of <see cref="CsvOptions"/> for multiple reads or writes.
    /// </remarks>
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
        /// Gets or sets whether data should be trimmed when accessed.
        /// </summary>
        public bool TrimData { get; set; }

        /// <summary>
        /// Gets or sets the comparer to use when looking up header names.
        /// </summary>
        public IEqualityComparer<string> Comparer { get; set; }

        ///<summary>
        /// Gets or sets an indicator to the parser to expect a header row or not.
        ///</summary>
        public HeaderMode HeaderMode { get; set; } = HeaderMode.HeaderPresent;

        /// <summary>
        /// Gets or sets whether a row should be validated immediately that the column count matches the header count.
        /// </summary>
        public bool ValidateColumnCount { get; set; }

        /// <summary>
        /// Gets or sets whether an empty string is returned for a missing column.
        /// </summary>
        public bool ReturnEmptyForMissingColumn { get; set; }

        /// <summary>
        /// Can be used to use multiple names for a single column. (e.g. to allow "CategoryName", "Category Name", "Category-Name")
        /// </summary>
        /// <remarks>
        /// A group with no matches is ignored.
        /// </remarks>
        public ICollection<string[]> Aliases { get; set; }

        /// <summary>
        /// Respects new line (either \r\n or \n) characters inside field values enclosed in double quotes.
        /// </summary>
        public bool AllowNewLineInEnclosedFieldValues { get; set; }

        /// <summary>
        /// Allows the sequence "\"" to be a valid quoted value (in addition to the standard """")
        /// </summary>
        public bool AllowBackSlashToEscapeQuote { get; set; }

        /// <summary>
        /// Allows the single-quote character to be used to enclose field values
        /// </summary>
        public bool AllowSingleQuoteToEncloseFieldValues { get; set; }

        /// <summary>
        /// The new line string to use when multiline field values are read
        /// </summary>
        /// <remarks>
        /// Requires "AllowNewLineInEnclosedFieldValues" to be set to "true" for this to have any effect.
        /// </remarks>
        public string NewLine { get; set; } = Environment.NewLine;

        internal Regex Splitter { get; set; }
    }
}