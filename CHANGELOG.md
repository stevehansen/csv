# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.205] - 2025-10-21

### Added
- AutoRenameHeaders option to handle duplicate and empty headers (#95)
  - Automatically renames duplicate headers (e.g., "A" becomes "A", "A2", "A3")
  - Converts empty headers to "Empty", "Empty2", etc.
  - Defaults to true; set to false to throw on duplicates (previous behavior)

### Changed
- Relaxed performance test thresholds for CI environments

## [2.0.170] - 2025-05-20

### Added
- .NET 9 support in tests and CodeQL workflow (#91, #92)
- AllowEnclosedFieldValues option (#51, #84)
- LineHasColumn method to check if a column exists (#32, #90)
- WriteAsync overload with CancellationToken support (#76)
- Helper extension methods: GetColumn and GetBlock
- Comprehensive test coverage for:
  - Comma inside quoted text (#73, #86)
  - Invalid CSV scenarios (#87)
  - Async operations (#88)
  - Skip-header functionality (#88)

### Fixed
- Writer null cell handling (#74, #83)
- Environment.NewLine usage in writer tests (#89)
- Header count for HeaderAbsent mode with multiline fields (#72)

### Changed
- Removed regex splitting in favor of more efficient parsing (#82)
- Expanded README with async and helper APIs documentation (#85)
- Updated target frameworks and NuGet properties

### Performance
- Eliminated regex usage in CSV parsing for better performance

## [2.0.128] - 2025-02-20

### Added
- .NET 8.0 target framework support
- Trimming and AOT (Ahead-of-Time compilation) support
- Collection expressions support
- Additional .NET 8 method overloads

### Changed
- Deprecated .NET Standard 1.0 target (no longer recommended)
- Fixed NuSpec paths
- Code cleanup: warnings and whitespace
- Updated dependencies:
  - actions/checkout to v4
  - actions/stale to v9
  - github/codeql-action to v3

### Performance
- Added Span/Memory optimizations for .NET 8.0+

## [2.0.93] - 2022-12-10

### Changed
- Updated Microsoft.NET.Test.Sdk to v17.4.0 (#55)

## [2.0.87] - 2022-09-02

### Added
- Async methods for Write and WriteToText (#52)
- Support for IAsyncEnumerable<string[]>

### Fixed
- Various fixes for async write operations (#52)

## [2.0.84] - 2022-05-04

### Added
- GetColumn and GetBlock extension methods (#46)
  - GetColumn(int columnNo) and GetColumn<T>(int columnNo, Func<string, T>)
  - GetBlock(int row_start, int row_length, int col_start, int col_length)

### Changed
- Updated mstest monorepo to v2.2.10 (#40)
- Updated github/codeql-action to v2 (#45)

## [2.0.80] - 2022-04-21

### Changed
- Updated Microsoft.NET.Test.Sdk to v17 (#43)
- Updated multiple dependencies:
  - Microsoft.SourceLink.GitHub to v1.1.1 (#38)
  - actions/checkout to v3 (#41)
  - actions/stale to v5 (#42)
  - mstest monorepo to v2 (#44)

## [2.0.76] - 2022-04-21

### Changed
- Minor updates and dependency maintenance

## [2.0.67] - 2022-03-31

### Added
- Renovate configuration for automated dependency updates (#36)

## [2.0.65] - 2022-01-18

### Fixed
- Fixed issue #35 with proper test coverage

## [2.0.64] - 2021-12-26

### Added
- Option to skip header when writing CSV files

### Performance
- Improved performance for .Replace call

## [2.0.62] - 2020-12-24

### Changed
- Moved custom logic from SplitLine to CsvLineSplitter
- Code organization improvements

## [2.0.61] - 2020-12-23

### Added
- Span/Memory support for .NET Core 3.1 and .NET Standard 2.1
- .NET Core 3.1 support
- .NET Standard 2.1 target (enables use in Blazor WASM projects) (#29)

### Performance
- Prefer Span/Memory APIs for better performance on supported frameworks
- Improved performance for string operations

## Earlier Versions

### [1.x] - 2015-2020

The library was initially released with comprehensive CSV reading and writing functionality:

#### Core Features
- CSV reading from TextReader, Stream, and string
- CSV writing with header support
- Configurable separators with auto-detection
- Header row handling (present/absent modes)
- Quote and escape character handling
- Multiline field support (#24)
- Column access by name or index
- Custom row validation and skipping

#### Configuration Options (CsvOptions)
- RowsToSkip and SkipRow for filtering
- TrimData for whitespace handling
- Comparer for case-insensitive column names
- ValidateColumnCount for strict validation
- ReturnEmptyForMissingColumn for flexible access
- Aliases for alternative column names
- AllowNewLineInEnclosedFieldValues for multiline fields
- AllowBackSlashToEscapeQuote for escape sequences
- AllowSingleQuoteToEncloseFieldValues for single-quote support

#### Additional Functionality
- SourceLink support (2018-08-28)
- CsvWriter class for generating CSV output (2018-02-14)
- Values property to retrieve all row values (#16)
- codecov integration (2019-02-17)
- CodeQL analysis (2020-10-02)

#### Bug Fixes
- Fixed issues #10, #11, #14, #19, #28, #35
- Fixed multiline parsing bugs (#24)
- Fixed unterminated quoted value detection

#### Framework Support
- Initial targets: .NET Standard 1.3, .NET 4.0
- Added .NET Standard 2.0 support
- Added .NET Standard 2.1 support (2020)
- Added .NET Core 3.1 support (2020)

---

For more details on each release, see the [commit history](https://github.com/stevehansen/csv/commits/master) or visit the [NuGet package page](https://www.nuget.org/packages/Csv/).

[2.0.205]: https://www.nuget.org/packages/Csv/2.0.205
[2.0.170]: https://www.nuget.org/packages/Csv/2.0.170
[2.0.128]: https://www.nuget.org/packages/Csv/2.0.128
[2.0.93]: https://www.nuget.org/packages/Csv/2.0.93
[2.0.87]: https://www.nuget.org/packages/Csv/2.0.87
[2.0.84]: https://www.nuget.org/packages/Csv/2.0.84
[2.0.80]: https://www.nuget.org/packages/Csv/2.0.80
[2.0.76]: https://www.nuget.org/packages/Csv/2.0.76
[2.0.67]: https://www.nuget.org/packages/Csv/2.0.67
[2.0.65]: https://www.nuget.org/packages/Csv/2.0.65
[2.0.64]: https://www.nuget.org/packages/Csv/2.0.64
[2.0.62]: https://www.nuget.org/packages/Csv/2.0.62
[2.0.61]: https://www.nuget.org/packages/Csv/2.0.61
