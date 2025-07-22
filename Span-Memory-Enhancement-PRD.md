# Product Requirements Document: Span/Memory-First CSV Library Enhancement

## Executive Summary

Enhance the existing CSV library to achieve maximum memory efficiency by implementing comprehensive Span<T>/Memory<T> support throughout the entire codebase, reducing string allocations and improving performance for high-throughput CSV processing scenarios.

## Current State Analysis

### Existing Span/Memory Support
The library currently has **partial** Span/Memory support:

**✅ Already Implemented:**
- `CsvReader.ReadFromMemory()` - Memory-based CSV reading (NET8_0_OR_GREATER only)
- `ICsvLineFromMemory` interface for memory-efficient row access
- `CsvLineSplitter` uses conditional compilation for Span/Memory vs string operations
- `StringHelpers` extension methods for `ReadOnlyMemory<char>` operations
- Memory-efficient line splitting in `CsvLineSplitter.Split()`

**❌ Current Limitations:**
- **CsvWriter**: Exclusively uses `string[]` arrays and `IEnumerable<string[]>`
- **ICsvLine**: Returns `string[]` for Headers/Values properties
- **Legacy compatibility**: Fallback to string-based operations for older frameworks
- **Mixed allocation patterns**: Some operations still create intermediate strings
- **Writer performance**: No Memory/Span-based writing capabilities

## Problem Statement

### Performance Issues
1. **Excessive String Allocations**: Writer creates new strings for every cell and row
2. **Array Copying**: Headers and values are copied between string arrays and Memory types
3. **Framework Fragmentation**: Inconsistent memory handling between .NET versions
4. **Large Data Processing**: Poor performance when processing large CSV files (>100MB)

### Memory Efficiency Gaps
- CsvWriter allocates approximately **3x more memory** than necessary due to string intermediate steps
- Header processing creates duplicate string arrays in multiple formats
- No vectorized operations for common CSV processing tasks

## Goals & Success Metrics

### Primary Goals
1. **Memory Reduction**: Reduce total memory allocations by 60-80% for large file processing
2. **Performance Improvement**: Achieve 2-3x performance improvement for read/write operations
3. **API Consistency**: Unified Span/Memory API surface across all components
4. **Backward Compatibility**: Maintain existing public API contracts

### Success Metrics
- **Benchmark**: Process 1GB CSV file with 50% less memory usage
- **Throughput**: Achieve >500MB/s processing speed on modern hardware  
- **Allocations**: Reduce gen0/gen1 garbage collections by 70%
- **API Coverage**: 100% of public APIs support Memory/Span variants

## Technical Requirements

### 1. Enhanced Writer API

#### New Span/Memory-Based Writer Methods
```csharp
// Core Memory-based writing
public static void Write(TextWriter writer, ReadOnlySpan<char> headers, IEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',');
public static void Write(TextWriter writer, ReadOnlyMemory<char>[] headers, IEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',');

// Async variants
public static Task WriteAsync(TextWriter writer, ReadOnlyMemory<char>[] headers, IAsyncEnumerable<ReadOnlyMemory<char>[]> lines, char separator = ',');

// High-performance buffer-based writing
public static void WriteToBuffer(Span<char> buffer, ReadOnlySpan<ReadOnlyMemory<char>> headers, ReadOnlySpan<ReadOnlyMemory<char>[]> lines, char separator, out int written);
```

#### Specialized Performance Writers
```csharp
// Vectorized writing for numeric data
public static void WriteNumeric<T>(TextWriter writer, ReadOnlySpan<string> headers, IEnumerable<ReadOnlySpan<T>> rows) where T : unmanaged;

// Zero-allocation streaming writer
public static IBufferWriter<char> CreateBufferWriter(ReadOnlySpan<char> headers, char separator);
```

### 2. Unified Memory-First Interfaces

#### Enhanced ICsvLine Interface
```csharp
public interface ICsvLineSpan : ICsvLine
{
    ReadOnlySpan<char> HeadersSpan { get; }
    ReadOnlySpan<char> ValuesSpan { get; }
    ReadOnlySpan<char> RawSpan { get; }
    ReadOnlySpan<char> this[string name] { get; }
    ReadOnlySpan<char> this[int index] { get; }
}
```

#### Memory Pool Integration
```csharp
public sealed class CsvMemoryOptions
{
    public MemoryPool<char>? MemoryPool { get; set; }
    public int InitialBufferSize { get; set; } = 4096;
    public bool ReuseBuffers { get; set; } = true;
}
```

### 3. Performance-Optimized Line Processing

#### Vectorized Operations
- SIMD-based separator detection
- Vectorized quote/escape character processing
- Parallel line splitting for multi-core scenarios

#### Buffer Management
- Pooled buffer allocation using `ArrayPool<T>`
- Configurable buffer sizes based on data characteristics
- Automatic buffer growth with geometric expansion

### 4. Advanced Memory Features

#### Streaming Enumerators
```csharp
public static IAsyncEnumerable<ReadOnlyMemory<char>[]> ReadAsMemoryAsync(Stream stream, CsvMemoryOptions? options = null);
public static IEnumerable<ReadOnlyMemory<char>[]> ReadAsMemory(ReadOnlySpan<char> csv, CsvMemoryOptions? options = null);
```

#### Zero-Copy Parsing
- Direct parsing from file-mapped memory
- In-place unescaping when possible  
- Reference-based column access without copying

## Implementation Plan

### Phase 1: Writer Enhancement (4 weeks)
1. **Week 1**: Implement core Memory-based CsvWriter methods
2. **Week 2**: Add async Memory/Span writer variants
3. **Week 3**: Optimize buffer management and pooling
4. **Week 4**: Performance testing and benchmarking

### Phase 2: Reader Unification (3 weeks)
1. **Week 1**: Extend ICsvLine with Span-based interface
2. **Week 2**: Implement unified Memory/Span reader pipeline
3. **Week 3**: Add vectorized parsing optimizations

### Phase 3: Advanced Features (3 weeks)
1. **Week 1**: Memory pool integration and buffer management
2. **Week 2**: Zero-copy parsing implementation
3. **Week 3**: SIMD optimizations and parallel processing

### Phase 4: Testing & Documentation (2 weeks)
1. **Week 1**: Comprehensive performance benchmarking
2. **Week 2**: Documentation and migration guides

## Technical Considerations

### Framework Support
- **Primary Target**: NET8.0+ for full Span/Memory feature support
- **Compatibility**: Maintain string-based fallbacks for netstandard2.0
- **Future**: Consider NET9.0+ specific optimizations

### Breaking Changes
- **Public API**: No breaking changes to existing methods
- **Performance**: Some existing usage patterns may see different memory characteristics
- **Dependencies**: May require newer System.Memory package versions

### Risk Mitigation
1. **Extensive benchmarking** across different data sizes and patterns
2. **Backward compatibility testing** with existing codebases  
3. **Memory leak detection** in long-running scenarios
4. **Cross-platform validation** on different runtime environments

## Success Criteria

### Performance Benchmarks
- **Large File (1GB+)**: 60%+ reduction in memory usage
- **Throughput**: 2-3x improvement in MB/s processing speed  
- **Latency**: 50%+ reduction in processing time for typical workflows
- **Allocations**: 70%+ reduction in managed heap allocations

### Quality Gates
- **Unit Tests**: 100% code coverage for new Span/Memory paths
- **Integration Tests**: Validate against existing CSV parsing test suite
- **Performance Tests**: Automated benchmarks with regression detection
- **Compatibility Tests**: Verify backward compatibility with existing APIs

## Implementation Notes

This enhancement represents a significant evolution toward a truly memory-efficient CSV processing library. The implementation should prioritize zero-allocation patterns where possible while maintaining the library's reputation for simplicity and reliability.

Key architectural principles:
- **Memory-first design**: Span/Memory types should be the primary API surface
- **Performance by default**: Optimized paths should be the standard, not opt-in
- **Compatibility preservation**: Existing code should work unchanged
- **Measurable improvements**: All optimizations should be validated with benchmarks