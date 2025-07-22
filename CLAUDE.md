# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET CSV parsing and writing library (`Csv`) that supports multiple target frameworks (netstandard2.0, net8.0, net9.0). The library provides simple and efficient CSV reading and writing capabilities with extensive configuration options.

## Architecture

### Core Components

- **`CsvReader`** (`Csv/CsvReader.cs`) - Main static class for reading CSV data from various sources (TextReader, Stream, string, ReadOnlyMemory<char>)
- **`CsvWriter`** (`Csv/CsvWriter.cs`) - Static class for writing CSV data to various outputs
- **`CsvOptions`** (`Csv/CsvOptions.cs`) - Configuration class containing parsing options (separators, headers, validation, etc.)
- **`ICsvLine`/`ICsvLineFromMemory`** (`Csv/ICsvLine.cs`, `Csv/ICsvLineFromMemory.cs`) - Interfaces for accessing CSV row data by index or column name
- **`CsvLineSplitter`** (`Csv/CsvLineSplitter.cs`) - Low-level CSV line parsing logic
- **`HeaderMode`** (`Csv/HeaderMode.cs`) - Enum defining header handling modes

### Project Structure

- **`Csv/`** - Main library project containing all CSV functionality
- **`Csv.Tests/`** - MSTest-based unit tests for the library
- **Solution** - Standard .NET solution with library + tests projects

### Key Features

- Multi-framework targeting (netstandard2.0, net8.0, net9.0)
- Async support (`IAsyncEnumerable` for .NET Standard 2.1+)
- Memory-efficient parsing with `ReadOnlyMemory<char>` support
- Extensive configuration options through `CsvOptions`
- Header-aware parsing with column name access
- Quote handling and escape sequences
- Custom separators with auto-detection

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

## Configuration

CSV parsing behavior is controlled through `CsvOptions` which includes:
- Separator detection/specification
- Header handling modes
- Quote and escape handling
- Memory vs string parsing
- Row skipping and validation
- Multiline field support