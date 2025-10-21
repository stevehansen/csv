# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET CSV parsing and writing library (`Csv`) that supports multiple target frameworks (netstandard2.0, net8.0, net9.0). The library provides simple and efficient CSV reading and writing capabilities with extensive configuration options.

## Architecture

### Core Components

- **`CsvReader`** (`Csv/CsvReader.cs`) - Main static class for reading CSV data from various sources (TextReader, Stream, string, ReadOnlyMemory<char>)
  - Contains multiple implementations: standard `Read*`, `ReadAsSpan*` (NET8.0+), and `ReadAsync*` methods
  - Inner classes: `ReadLine` (standard), `ReadLineSpan` (NET8.0+), `ReadLineSpanOptimized` (memory-optimized)
- **`CsvReader.FromMemory.cs`** - Partial class containing memory-optimized reading methods
- **`CsvWriter`** (`Csv/CsvWriter.cs`) - Static class for writing CSV data to various outputs
- **`CsvOptions`** (`Csv/CsvOptions.cs`) - Configuration class containing parsing options (separators, headers, validation, etc.)
- **`CsvMemoryOptions`** (`Csv/CsvMemoryOptions.cs`) - Memory management options for optimized parsing (NET8.0+)
- **`ICsvLine`** (`Csv/ICsvLine.cs`) - Interface for accessing CSV row data by index or column name (string-based)
- **`ICsvLineSpan`** (`Csv/ICsvLineSpan.cs`) - Interface extending ICsvLine with Span/Memory support (NET8.0+)
- **`ICsvLineFromMemory`** (`Csv/ICsvLineFromMemory.cs`) - Legacy memory-based interface
- **`CsvLineSplitter`** (`Csv/CsvLineSplitter.cs`) - Low-level CSV line parsing logic
  - Key method: `IsUnterminatedQuotedValue` - detects multiline field continuation
- **`CsvBufferWriter`** (`Csv/CsvBufferWriter.cs`) - Buffer-based CSV writer for optimized writing (NET8.0+)
- **`StringHelpers`** (`Csv/StringHelpers.cs`) - String manipulation utilities including unescape operations
- **`HeaderMode`** (`Csv/HeaderMode.cs`) - Enum defining header handling modes (HeaderPresent, HeaderAbsent)

### Project Structure

- **`Csv/`** - Main library project containing all CSV functionality
- **`Csv.Tests/`** - MSTest-based unit tests for the library
- **`docs/`** - Documentation including PRDs (Product Requirement Documents) and implementation guides
- **Solution** - Standard .NET solution with library + tests projects

### Key Features & Conditional Compilation

- Multi-framework targeting (netstandard2.0, net8.0, net9.0)
- Async support (`IAsyncEnumerable` for NET8.0+)
- Memory-efficient parsing with `ReadOnlyMemory<char>` and `ReadOnlySpan<char>` support (NET8.0+)
- Extensive configuration options through `CsvOptions`
- Header-aware parsing with column name access
- Quote handling and escape sequences
- Custom separators with auto-detection
- Multiline field support via `AllowNewLineInEnclosedFieldValues`
- AOT and trimming compatible (NET8.0+)

**Important**: The codebase uses `#if NET8_0_OR_GREATER` directives extensively. Modern Span/Memory APIs are only available for NET8.0+, while netstandard2.0 uses string-based equivalents with type aliases (`MemoryText` and `SpanText`).

## Development Commands

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Run Specific Test
```bash
dotnet test --filter "TestMethodName"
```

### Create NuGet Package
```bash
dotnet pack
```

### Build for Specific Framework
```bash
dotnet build -f net8.0
```

## Testing

The project uses MSTest framework with test files in `Csv.Tests/`:
- `Tests.cs` - Main CSV parsing tests
- `WriterTests.cs` - CSV writer tests
- `IssuesTests.cs` - Regression tests for reported issues
- `MemoryTests.cs` - Memory-specific parsing tests
- `SpanMemoryTests.cs` - Span/Memory API tests (NET8.0+)
- `RegexEliminationTests.cs` - Tests for regex-free parsing
- `PerformanceTests.cs` / `RegexPerformanceTests.cs` - Performance benchmarks

The tests target net9.0. When adding tests for framework-specific features (NET8.0+ Span/Memory APIs), use appropriate `#if NET8_0_OR_GREATER` directives.

## Configuration

CSV parsing behavior is controlled through `CsvOptions` which includes:
- Separator detection/specification
- Header handling modes (HeaderPresent, HeaderAbsent)
- Quote and escape handling (`AllowEnclosedFieldValues`, `AllowBackSlashToEscapeQuote`, `AllowSingleQuoteToEncloseFieldValues`)
- Row skipping and validation (`RowsToSkip`, `SkipRow`, `ValidateColumnCount`)
- Multiline field support (`AllowNewLineInEnclosedFieldValues`)
- Column aliases and case-insensitive matching
- Memory vs string parsing (standard vs Span/Memory APIs)

## Implementation Details

### Multiline Field Handling
The library supports CSV fields that span multiple lines when enclosed in quotes. The logic works by:
1. `CsvLineSplitter.IsUnterminatedQuotedValue()` checks if a field's quotes are unbalanced
2. When detected, `CsvReader` continues reading lines and concatenates them
3. Special handling exists for `HeaderAbsent` mode to avoid re-processing the first data line

### Header Processing
- **HeaderPresent** (default): First non-skipped row is treated as headers; duplicate headers cause an error
- **HeaderAbsent**: Auto-generates column names as "Column1", "Column2", etc.
- Aliases can be configured to provide alternative names for the same column

### Performance Optimizations
- NET8.0+ builds use `ReadOnlySpan<char>` and `ReadOnlyMemory<char>` to minimize allocations
- `CsvMemoryOptions` provides ArrayPool-based buffering for high-performance scenarios
- Type aliases (`MemoryText`/`SpanText`) allow code sharing between netstandard2.0 and NET8.0+
- AOT and trimming attributes are set for NET8.0+ builds

## Documentation Guidelines

### PRDs (Product Requirement Documents)
- All PRDs should be placed in the `docs/` folder
- Use descriptive names like `Feature-Name-PRD.md`
- Include implementation documents alongside PRDs when available
- Examples: `Span-Memory-PRD.md`, `Regex-Elimination-Enhancement-PRD.md`