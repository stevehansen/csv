# csv

[![NuGet Version](https://img.shields.io/nuget/v/Csv.svg?style=flat)](https://www.nuget.org/packages/Csv/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Csv.svg?style=flat)](https://www.nuget.org/packages/Csv/)
[![Build status](https://ci.appveyor.com/api/projects/status/d1m0vu1n7idsk7uu?svg=true)](https://ci.appveyor.com/project/SteveHansen/csv)
[![codecov](https://codecov.io/gh/stevehansen/csv/branch/master/graph/badge.svg)](https://codecov.io/gh/stevehansen/csv)

Really simple csv library

This library targets .NET Standard 2.0, .NET 8.0, and .NET 9.0, with enhanced Span/Memory APIs available for .NET 8.0+.

## Install

To install csv, use the following command in the Package Manager Console

    PM> Install-Package Csv

## Changelog

For a detailed list of changes and version history, see [CHANGELOG.md](CHANGELOG.md).

## Basic Usage

_More examples can be found in the tests._

### Reading a CSV file

```csharp
// NOTE: Library assumes that the csv data will have a header row by default, see CsvOptions.HeaderMode
/*
# comments are ignored
Column name,Second column,Third column
First cell,second cell,
Second row,second cell,third cell
*/ 
var csv = File.ReadAllText("sample.csv");
foreach (var line in CsvReader.ReadFromText(csv))
{
    // Header is handled, each line will contain the actual row data
    var firstCell = line[0];
    var byName = line["Column name"];
}
```

`CsvReader` also supports reading from a `TextReader` (`CsvReader.Read(TextReader, CsvOptions)`) or a `Stream` (`CsvReader.ReadFromStream(Stream, CsvOptions)`)

For .NET 8.0+ the library exposes:
- `CsvReader.ReadAsync` and `CsvReader.ReadFromStreamAsync` which return `IAsyncEnumerable<ICsvLine>`
- `CsvReader.ReadFromMemory` to read from a `ReadOnlyMemory<char>` without allocating intermediate strings
- `CsvReader.ReadAsSpan`, `CsvReader.ReadFromStreamAsSpan`, and `CsvReader.ReadFromTextAsSpan` which return `IEnumerable<ICsvLineSpan>` for zero-allocation Span/Memory access to CSV data
- `CsvReader.ReadFromMemoryOptimized` with `CsvMemoryOptions` for high-performance scenarios using ArrayPool-based buffering

#### High-performance reading with Span/Memory (.NET 8.0+)

```csharp
var csv = File.ReadAllText("sample.csv");
foreach (var line in CsvReader.ReadFromTextAsSpan(csv))
{
    // Access data as ReadOnlySpan<char> for zero allocations
    ReadOnlySpan<char> firstCell = line.GetSpan(0);
    ReadOnlySpan<char> byName = line.GetSpan("Column name");

    // Or as ReadOnlyMemory<char> if you need to store references
    ReadOnlyMemory<char> cellMemory = line.GetMemory(0);
}
```

`CsvOptions` can be used to configure the csv parsing:

```csharp
var options = new CsvOptions // Defaults
{
    RowsToSkip = 0, // Allows skipping of initial rows without csv data
    SkipRow = (row, idx) => string.IsNullOrEmpty(row) || row[0] == '#',
    Separator = '\0', // Autodetects based on first row
    TrimData = false, // Can be used to trim each cell
    Comparer = null, // Can be used for case-insensitive comparison for names
    HeaderMode = HeaderMode.HeaderPresent, // Assumes first row is a header row
    AutoRenameHeaders = true, // Automatically renames duplicate headers (e.g., "A", "A2", "A3") and converts empty headers to "Empty", "Empty2", etc. Set to false to throw on duplicates.
    ValidateColumnCount = false, // Checks each row immediately for column count
    ReturnEmptyForMissingColumn = false, // Allows for accessing invalid column names
    Aliases = null, // A collection of alternative column names
    AllowNewLineInEnclosedFieldValues = false, // Respects new line (either \r\n or \n) characters inside field values enclosed in double quotes.
    AllowBackSlashToEscapeQuote = false, // Allows the sequence "\"" to be a valid quoted value (in addition to the standard """")
    AllowSingleQuoteToEncloseFieldValues = false, // Allows the single-quote character to be used to enclose field values
    NewLine = Environment.NewLine // The new line string to use when multiline field values are read (Requires "AllowNewLineInEnclosedFieldValues" to be set to "true" for this to have any effect.)
};
```

### Writing a CSV file

#### With headers

```csharp
var columnNames = new [] { "Id", "Name" };
var rows = new []
{
    new [] { "0", "John Doe" },
    new [] { "1", "Jane Doe" }
};
var csv = CsvWriter.WriteToText(columnNames, rows, ',');
File.WriteAllText("people.csv", csv);
/*
Writes the following to the file:

Id,Name
0,John Doe
1,Jane Doe
*/
```

#### Without headers

```csharp
var rows = new []
{
    new [] { "0", "John Doe" },
    new [] { "1", "Jane Doe" }
};
// Convenience overload - no need to pass headers or skipHeaderRow
var csv = CsvWriter.WriteToText(rows);
File.WriteAllText("people.csv", csv);
/*
Writes the following to the file (no header row):

0,John Doe
1,Jane Doe
*/
```

#### Skipping headers

```csharp
var columnNames = new [] { "Id", "Name" };
var rows = new []
{
    new [] { "0", "John Doe" },
    new [] { "1", "Jane Doe" }
};
// Pass null for headers and skipHeaderRow: true
// Column count is determined from the first data row
var csv = CsvWriter.WriteToText(null, rows, ',', skipHeaderRow: true);
```

#### Custom separator

```csharp
var rows = new [] { new [] { "A", "B" }, new [] { "C", "D" } };
var csv = CsvWriter.WriteToText(rows, ';'); // semicolon separator
// Output: A;B
//         C;D
```

`CsvWriter` also includes asynchronous overloads (`WriteAsync` and
`WriteToTextAsync`) which operate on `IAsyncEnumerable<string[]>` and support
passing a `CancellationToken`. For .NET 8.0+, memory-efficient overloads using
`ReadOnlyMemory<char>` are available.

### Helper extensions

`CsvReader` provides extension methods to work with the returned
`IEnumerable<ICsvLine>`:

- `GetColumn(int columnNo)` / `GetColumn<T>(int columnNo, Func<string, T>)` –
  extract a single column from all rows.
- `GetBlock(int row_start = 0, int row_length = -1, int col_start = 0,
  int col_length = -1)` – get a rectangular subset of the data.


## Status

[![NuGet Version](https://img.shields.io/nuget/v/Csv.svg?style=flat)](https://www.nuget.org/packages/Csv/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Csv.svg?style=flat)](https://www.nuget.org/packages/Csv/)
[![Build status](https://ci.appveyor.com/api/projects/status/d1m0vu1n7idsk7uu?svg=true)](https://ci.appveyor.com/project/SteveHansen/csv)
[![codecov](https://codecov.io/gh/stevehansen/csv/branch/master/graph/badge.svg)](https://codecov.io/gh/stevehansen/csv)
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv.svg?type=shield)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv?ref=badge_shield)


## License
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv.svg?type=large)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv?ref=badge_large)
