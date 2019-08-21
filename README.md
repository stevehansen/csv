# csv
Really simple csv library

## Install

To install csv, use the following command in the Package Manager Console

    PM> Install-Package Csv

## Basic Usage

_More examples can be found in the tests._

### Reading a CSV file

```csharp
// NOTE: Library assumes that the csv data will have a header row
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


## Build status
[![Build status](https://ci.appveyor.com/api/projects/status/d1m0vu1n7idsk7uu?svg=true)](https://ci.appveyor.com/project/SteveHansen/csv)
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv.svg?type=shield)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv?ref=badge_shield)
[![codecov](https://codecov.io/gh/stevehansen/csv/branch/master/graph/badge.svg)](https://codecov.io/gh/stevehansen/csv)


## License
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv.svg?type=large)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv?ref=badge_large)
