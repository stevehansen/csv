# CSV Regex Elimination Enhancement - PRD

## Executive Summary

This PRD outlines the plan to eliminate regex usage from the CSV library to achieve significant performance improvements, particularly in high-throughput scenarios involving multiline quoted fields. The current implementation uses regex for quote termination detection, which creates performance bottlenecks and memory allocation overhead.

## Problem Statement

### Current Performance Bottlenecks

1. **Regex Pattern Creation**: Dynamic regex patterns are generated for each quote character configuration
2. **String Allocation Overhead**: Span<char> to string conversion required for regex operations in .NET 8+
3. **Regex Execution Cost**: Pattern matching overhead for every field containing quotes
4. **Memory Pressure**: Additional allocations during regex matching operations

### Performance Impact Data

- **Hot Path Location**: `CsvLineSplitter.IsUnterminatedQuotedValue()` method
- **Frequency**: Called for every field when `AllowNewLineInEnclosedFieldValues` is enabled
- **Current Pattern**: `@"\\?{quoteChar}+$"` or `@"{quoteChar}+$"` depending on escape settings
- **Scaling**: O(n) per field containing quotes, where n = characters after first quote

## Goals & Success Criteria

### Primary Goals

1. **Eliminate All Regex Usage**: Replace regex-based quote detection with character-based parsing
2. **Performance Improvement**: Target 30-50% improvement in multiline quoted field processing
3. **Memory Reduction**: Eliminate string allocations from Span<char> conversions
4. **Maintain Compatibility**: Preserve all existing CSV parsing behavior and options

### Success Metrics

- **Benchmark Results**: 
  - 30%+ improvement in processing CSV files with multiline quoted fields
  - 25%+ reduction in memory allocations during quote parsing
  - Zero regex-related allocations in performance profiling
- **Functional Testing**: All existing tests pass without modification
- **Compliance**: Maintain RFC 4180 compliance for CSV parsing

## Technical Requirements

### Functional Requirements

1. **Quote Termination Detection**: Replace regex with character-based parsing
   - Support double quotes (`"`) and single quotes (`'`)
   - Handle backslash-escaped quotes when `AllowBackSlashToEscapeQuote` is enabled
   - Properly detect unterminated quoted values

2. **Cross-Framework Compatibility**: 
   - Maintain support for NET Standard 2.0+, NET 8.0+
   - Optimize for Span<char> operations where available
   - Fallback to string operations for older frameworks

3. **Configuration Support**:
   - Support all existing CSV options and configurations
   - Maintain backward compatibility with existing APIs

### Performance Requirements

1. **Processing Speed**: 30-50% improvement in quote parsing performance
2. **Memory Efficiency**: Eliminate unnecessary string allocations
3. **Scalability**: O(1) or O(k) complexity where k is number of trailing quotes

### Technical Constraints

1. **No Breaking Changes**: All public APIs must remain unchanged
2. **Behavioral Compatibility**: Exact same parsing results as current regex implementation
3. **Framework Support**: Must work across all supported .NET versions

## Proposed Solution

### Phase 1: Character-Based Quote Parser Implementation

**New Implementation Strategy:**

```csharp
// Replace regex-based quote detection with efficient character parsing
private static bool IsUnterminatedQuotedValueOptimized(ReadOnlySpan<char> value, char quoteChar, bool allowBackslashEscape)
{
    if (value.Length <= 1 || value[0] != quoteChar)
        return false;

    // Scan from end to find trailing quotes
    int trailingQuoteCount = 0;
    int index = value.Length - 1;
    
    // Count consecutive quotes from the end
    while (index >= 1 && value[index] == quoteChar)
    {
        trailingQuoteCount++;
        index--;
    }
    
    // Handle backslash escaping if enabled
    if (allowBackslashEscape && index >= 0 && value[index] == '\\')
    {
        // Check if the backslash itself is escaped
        int backslashCount = 0;
        while (index >= 0 && value[index] == '\\')
        {
            backslashCount++;
            index--;
        }
        
        // If odd number of backslashes, the quote is escaped
        if (backslashCount % 2 == 1)
        {
            trailingQuoteCount = Math.Max(0, trailingQuoteCount - 1);
        }
    }
    
    // Odd number of quotes = properly terminated, even = unterminated
    return trailingQuoteCount % 2 == 0;
}
```

**Key Optimizations:**
1. **No String Allocation**: Work directly with Span<char>
2. **Single Pass**: Scan from end to count trailing quotes
3. **Efficient Escaping**: Handle backslash escapes without regex
4. **Early Termination**: Exit as soon as non-quote character found

### Phase 2: Integration and Optimization

1. **Replace Current Implementation**:
   - Update `CsvLineSplitter.IsUnterminatedQuotedValue()` method
   - Remove regex dependency from `StringHelpers.cs`
   - Update all call sites to use new implementation

2. **Cross-Framework Optimization**:
   ```csharp
   #if NET8_0_OR_GREATER
       return IsUnterminatedQuotedValueOptimized(value[1..], quoteChar, allowBackslashEscape);
   #else
       return IsUnterminatedQuotedValueOptimized(value.AsSpan(1), quoteChar, allowBackslashEscape);
   #endif
   ```

3. **Performance Validation**:
   - Comprehensive benchmarking against current regex implementation
   - Memory allocation profiling
   - Edge case validation testing

### Phase 3: Testing and Validation

1. **Unit Tests**:
   - Test all quote parsing scenarios
   - Validate backslash escaping behavior
   - Test edge cases (empty strings, single quotes, etc.)

2. **Performance Tests**:
   - Benchmark quote parsing performance improvements
   - Memory allocation comparison tests
   - Large dataset processing benchmarks

3. **Compatibility Tests**:
   - Ensure all existing CSV parsing tests pass
   - Validate behavior matches regex implementation exactly
   - Cross-framework compatibility verification

## Implementation Plan

### Phase 1: Core Implementation (Week 1)
- [ ] Implement character-based quote parser
- [ ] Create comprehensive unit tests for new parser
- [ ] Validate exact behavior match with current regex implementation

### Phase 2: Integration (Week 2)
- [ ] Replace regex usage in `CsvLineSplitter.IsUnterminatedQuotedValue()`
- [ ] Remove regex dependencies from `StringHelpers.cs`
- [ ] Update cross-framework compatibility code

### Phase 3: Testing and Optimization (Week 3)
- [ ] Run comprehensive test suite validation
- [ ] Create performance benchmarks comparing old vs new implementation
- [ ] Optimize implementation based on benchmark results

### Phase 4: Documentation and Validation (Week 4)
- [ ] Update CLAUDE.md with performance improvements
- [ ] Document new implementation approach
- [ ] Final validation and performance verification

## Risk Assessment

### Low Risk
- **Implementation Complexity**: Character-based parsing is straightforward
- **Behavioral Changes**: Can maintain exact same parsing logic

### Medium Risk
- **Performance Validation**: Need thorough benchmarking to ensure improvements
- **Edge Case Handling**: Must handle all regex edge cases correctly

### Mitigation Strategies
- **Comprehensive Testing**: Create exhaustive test cases covering all scenarios
- **Gradual Rollout**: Implement behind feature flag initially
- **Benchmark Validation**: Continuous performance monitoring during implementation

## Dependencies

### Internal Dependencies
- Access to existing CSV parsing test suite
- Performance benchmarking infrastructure
- Cross-framework testing capabilities

### External Dependencies
- None (removing regex dependency reduces external dependencies)

## Metrics and KPIs

### Performance Metrics
- **Quote Parsing Speed**: 30-50% improvement target
- **Memory Allocations**: 25%+ reduction in quote parsing operations
- **Overall CSV Processing**: 10-15% improvement for files with multiline quoted fields

### Quality Metrics
- **Test Coverage**: Maintain 100% test coverage for quote parsing
- **Regression Tests**: Zero failures in existing test suite
- **Compatibility**: 100% behavioral compatibility with current implementation

## Conclusion

Eliminating regex usage from the CSV library presents a significant opportunity for performance improvement with minimal risk. The proposed character-based parsing approach will reduce memory allocations, improve processing speed, and maintain full compatibility with existing functionality. This enhancement aligns with the library's goal of providing high-performance CSV processing while maintaining reliability and standards compliance.