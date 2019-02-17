# csv
Really simple csv library

## Install

To install csv, use the following command in the Package Manager Console

    PM> Install-Package Csv

## Usage

```csharp
var csv = File.ReadAllText("sample.csv");
foreach (var line in CsvReader.ReadFromText(csv))
{
    // Header is handled, each line will contain the actual row data
    var firstCell = line[0];
    var byName = line["Column name"];
}
```

More examples can be found in the tests.


## Build status
[![Build status](https://ci.appveyor.com/api/projects/status/d1m0vu1n7idsk7uu?svg=true)](https://ci.appveyor.com/project/SteveHansen/csv)
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv.svg?type=shield)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv?ref=badge_shield)
[![codecov](https://codecov.io/gh/stevehansen/csv/branch/master/graph/badge.svg)](https://codecov.io/gh/stevehansen/csv)


## License
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv.svg?type=large)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fcsv?ref=badge_large)
