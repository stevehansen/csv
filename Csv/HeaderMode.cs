using System;

namespace Csv
{
    /// <summary>
    /// Indicates the presence or absence of a header row
    /// </summary>
    public enum HeaderMode
    {
        /// <summary>
        /// Indicates that the CSV file has a header row
        /// </summary>
        HeaderPresent,

        /// <summary>
        /// Indicates that the CSV file does not have a header row
        /// </summary>
        HeaderAbsent
    }
}