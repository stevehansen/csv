# STRIDE Threat Model - Csv Library

## Overview

This document presents a STRIDE threat model for the `Csv` .NET library. The library parses and writes CSV data from various sources (strings, streams, `TextReader`, `ReadOnlyMemory<char>`) and is intended for use as a dependency in applications that process potentially untrusted CSV input.

**Scope**: The library itself (not any specific consuming application).
**Version**: As of commit `80356b8` (master branch).

---

## System Description

### Trust Boundaries

1. **External Input Boundary** - CSV data enters the library from callers via `CsvReader.Read()`, `ReadFromStream()`, `ReadFromText()`, and related methods. The library does not control the origin of this data.
2. **Configuration Boundary** - `CsvOptions` and `CsvMemoryOptions` are provided by the caller. Misconfiguration can alter parsing behavior.
3. **Output Boundary** - `CsvWriter` produces CSV output that downstream consumers trust to be well-formed.

### Components

| Component | Role |
|---|---|
| `CsvReader` | Entry point for parsing CSV data |
| `CsvWriter` | Entry point for writing CSV data |
| `CsvLineSplitter` | Low-level line splitting and quote detection |
| `CsvOptions` | Configuration (separator, quoting, headers, multiline) |
| `CsvMemoryOptions` | Buffer pool configuration (NET8.0+) |
| `CsvBufferWriter` | Pool-based CSV writer (NET8.0+) |
| `StringHelpers` | Unescape and memory utility methods |

### Data Flow

```
Untrusted CSV Input
        |
        v
  CsvReader.Read*()
        |
        v
  InitializeOptions() --> AutoDetectSeparator()
        |
        v
  GetHeaders() / CreateDefaultHeaders()
        |
        v
  CsvLineSplitter.Split() --> IsUnterminatedQuotedValue()
        |
        v
  Trim() / Unescape()
        |
        v
  ICsvLine (consumed by application)
```

---

## Threat Analysis

### S - Spoofing

| ID | Threat | Severity | Description | Status |
|---|---|---|---|---|
| S-1 | Header spoofing via crafted CSV | Low | An attacker can craft CSV headers that mimic legitimate column names (e.g., "Amount" vs "Amount" with unicode homoglyphs). The library does not normalize unicode in header names. | Accepted - out of scope for a parsing library; consumers should validate headers. |
| S-2 | Auto-renamed headers obscure duplicates | Low | When `AutoRenameHeaders` is `true` (default), duplicate headers are silently renamed (e.g., "A", "A2", "A3"). A malicious CSV could include duplicate headers to cause the application to read from an unexpected column. | Mitigated - `AutoRenameHeaders` can be set to `false` to throw on duplicates. |

### T - Tampering

| ID | Threat | Severity | Description | Status |
|---|---|---|---|---|
| T-1 | Separator auto-detection manipulation | Medium | When `Separator` is `'\0'` (default), the library auto-detects from the header row by scanning for `;` or `\t`, falling back to `,`. A crafted first row could force an unintended separator, causing field values to be split or merged incorrectly. For example, a header like `"Name;Email,Phone"` would select `;` as the separator, causing `Email,Phone` to become a single column. | Partially mitigated - callers can explicitly set `Separator` to avoid auto-detection. |
| T-2 | Multiline field injection | Medium | With `AllowNewLineInEnclosedFieldValues` enabled, an attacker can craft fields that span many lines, potentially altering how subsequent rows are parsed. A maliciously unterminated quoted field could consume the rest of the input. The reader does break on EOF (`ReadLine` returning `null`), which limits the blast radius. | Partially mitigated - feature is opt-in (`false` by default). No configurable line limit exists for multiline fields. |
| T-3 | Quote escape confusion | Medium | The library supports multiple quote escape mechanisms (`""`, `\"`, single quotes). When `AllowBackSlashToEscapeQuote` is enabled alongside standard CSV double-quote escaping, ambiguous input like `"value\"",next` could be parsed differently than expected. | Partially mitigated - `AllowBackSlashToEscapeQuote` is opt-in. |
| T-4 | Writer output injection | Low | `CsvWriter` escapes separators, quotes, single quotes, and newlines. However, only `\n` is checked in the `escapeChars` array; `\r` alone (without `\n`) is not escaped by `WriteLine`/`WriteLineAsync` in the string-based writer. The `CsvBufferWriter.WriteCell` does include `\r`. This inconsistency could allow a bare `\r` to be written unquoted. | Open - minor inconsistency between writer implementations. |

### R - Repudiation

| ID | Threat | Severity | Description | Status |
|---|---|---|---|---|
| R-1 | No parsing audit trail | Informational | The library does not emit logs or diagnostics about parsing decisions (e.g., separator auto-detection result, skipped rows, multiline field concatenation). An application cannot audit how input was interpreted. | Accepted - logging is the responsibility of the consuming application. Libraries should not impose a logging framework. |

### I - Information Disclosure

| ID | Threat | Severity | Description | Status |
|---|---|---|---|---|
| I-1 | Pooled buffer data leakage | Medium | `CsvMemoryOptions.ClearBuffers` defaults to `false`. When buffers are returned to `ArrayPool<char>.Shared` without clearing, residual data from previous CSV parsing operations remains in the buffer. A subsequent consumer of the same pooled buffer (potentially in a different security context within the same process) could observe leftover data. | Partially mitigated - `ClearBuffers` option exists but defaults to `false` for performance. |
| I-2 | Error messages reveal header names | Low | Exception messages include header names and column counts (e.g., `"Header 'X' does not exist. Expected one of A; B; C"`). In a multi-tenant application, this could leak schema information from one tenant's CSV to another if exceptions are surfaced. | Accepted - standard .NET exception behavior; applications should catch and sanitize exceptions. |
| I-3 | `ICsvLine.Raw` exposes original input | Informational | The `Raw` property on `ICsvLine` retains the original unparsed line. If parsed data is passed downstream, the raw representation (which may include fields the consumer should not see) travels with it. | Accepted - by design for debugging and audit purposes. |

### D - Denial of Service

| ID | Threat | Severity | Description | Status |
|---|---|---|---|---|
| D-1 | Unbounded multiline field concatenation | High | When `AllowNewLineInEnclosedFieldValues` is `true`, a single unterminated opening quote causes the reader to concatenate every subsequent line via string concatenation (`line += options.NewLine + nextLine`). For a large input, this produces O(n^2) memory and CPU usage due to repeated string allocations. The reader only stops at EOF. There is no limit on the number of lines that can be concatenated. | Open - no configurable max field size or max lines per field. |
| D-2 | Large column count from crafted input | Medium | A single line with thousands of separator characters creates a large `List<MemoryText>` and corresponding header/value arrays. With `ValidateColumnCount` off (default), each row independently allocates arrays. | Partially mitigated - `ValidateColumnCount` can catch mismatched rows but not the initial explosion. |
| D-3 | `SkipRow` delegate on every row | Low | The `SkipRow` delegate is invoked for every row. A caller-supplied delegate with expensive logic could slow parsing, but this is caller-controlled, not attacker-controlled (unless the delegate itself depends on input). | Accepted - caller responsibility. |
| D-4 | `CsvBufferWriter` unbounded growth | Medium | `CsvBufferWriter.EnsureCapacity` allocates new buffers as needed, limited only by `MaxBufferSize` per individual buffer, but the number of buffers in `_buffers` list is unbounded. Writing very large CSV data could consume excessive memory. | Open - no total memory limit across all buffers. |
| D-5 | Regex-free but O(n) per-field scanning | Low | The library eliminated regex usage in favor of manual character scanning. While this removed regex-based ReDoS, the `IsUnterminatedQuotedValue` method still scans the full field length. For extremely long fields this is linear, not exponential, so the risk is low. | Mitigated - linear complexity is acceptable. |

### E - Elevation of Privilege

| ID | Threat | Severity | Description | Status |
|---|---|---|---|---|
| E-1 | Formula injection in output | Medium | `CsvWriter` does not sanitize cell values that begin with `=`, `+`, `-`, `@`, `\t`, or `\r`. When the output CSV is opened in spreadsheet software (Excel, Google Sheets, LibreOffice Calc), these prefixes can trigger formula execution, potentially leading to data exfiltration or command execution on the user's machine. This is commonly known as "CSV injection" or "formula injection". | Open - the library does not sanitize formula-triggering characters. This is a known industry-wide issue with CSV format and is typically addressed at the application layer. |
| E-2 | `SkipRow` delegate injection | Low | The `SkipRow` property accepts an arbitrary `Func<>`. A misconfigured application that allows user-controlled code to set this delegate could introduce arbitrary code execution during parsing. | Accepted - standard delegate pattern; callers control what code they pass. |

---

## Risk Summary

| Severity | Count | IDs |
|---|---|---|
| High | 1 | D-1 |
| Medium | 6 | T-1, T-2, T-3, I-1, D-2, D-4, E-1 |
| Low | 5 | S-1, S-2, T-4, I-2, D-3, E-2 |
| Informational | 2 | R-1, I-3 |

## Recommendations

### For Library Maintainers

1. **D-1**: Consider adding a configurable maximum field size or maximum lines per multiline field to `CsvOptions` (e.g., `MaxFieldLength`, `MaxLinesPerField`). This would protect against quadratic blowup from unterminated quotes.
2. **T-4**: Align the `\r` handling between `CsvWriter.WriteLine` (string-based) and `CsvBufferWriter.WriteCell` so both escape bare carriage returns.
3. **I-1**: Consider documenting the security implications of `ClearBuffers = false` more prominently, or changing the default to `true` for security-sensitive scenarios.
4. **D-4**: Consider adding a total memory limit to `CsvBufferWriter` (sum of all buffer sizes) and throwing when exceeded.

### For Library Consumers

1. **Always set `Separator` explicitly** when parsing untrusted input to avoid auto-detection manipulation (T-1).
2. **Use `ValidateColumnCount = true`** when the expected schema is known to detect malformed rows early (D-2).
3. **Set `AllowNewLineInEnclosedFieldValues` to `true` only when needed**, and be aware of the DoS risk with unterminated quotes (D-1).
4. **Sanitize cell values before writing** if the output CSV may be opened in spreadsheet software. Prefix cells starting with `=`, `+`, `-`, `@`, `\t`, `\r` with a single quote or tab character (E-1).
5. **Set `ClearBuffers = true`** in `CsvMemoryOptions` when processing sensitive data in multi-tenant or shared-memory scenarios (I-1).
6. **Catch and sanitize exceptions** before surfacing them to end users to avoid leaking header/schema information (I-2).
7. **Set `AutoRenameHeaders = false`** when strict header validation is required to detect duplicate headers as errors (S-2).
