# STRIDE Threat Model - Csv Library

## System Overview

This document presents a STRIDE threat model for the `Csv` .NET library. The library parses and writes CSV data from various sources (strings, streams, `TextReader`, `ReadOnlyMemory<char>`) and is intended for use as a dependency in applications that process potentially untrusted CSV input.

**Scope**: The library itself (not any specific consuming application).
**Version**: As of commit `b04efcc` (master branch, 2026-07-07).

### Trust Boundaries

1. **External Input Boundary** - CSV data enters the library from callers via `CsvReader.Read()`, `ReadFromStream()`, `ReadFromText()`, `ReadFromMemory()`, and related methods. The library does not control the origin of this data.
2. **Configuration Boundary** - `CsvOptions` and `CsvMemoryOptions` are provided by the caller. Misconfiguration can alter parsing behavior.
3. **Output Boundary** - `CsvWriter` produces CSV output that downstream consumers trust to be well-formed.

### Components

| Component | Role |
|---|---|
| `CsvReader` | Entry point for parsing CSV data |
| `CsvReader.Engine` | Unified read engine (`Enumerate`/`EnumerateAsync`) behind all read paths; line sources + row factories |
| `CsvLine<TPolicy>` | Single row type backing every read path (string, span, memory, optimized) |
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
  Enumerate<TSource, TFactory, TRow>()   (unified engine)
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

### Data Classification

The library handles arbitrary caller-supplied data; it stores nothing itself. Sensitivity is determined entirely by the consuming application (which may pass PII, financial data, or credentials through the parser). Assessed against **OWASP ASVS 5.0 Level 1** (library context; consuming applications handling confidential data should apply L2+ controls at the application layer).

---

## STRIDE Analysis

**Scoring**: Likelihood (1-4) × Impact (1-4) = Score. High priority = score >= 8.
**Control citations**: OWASP ASVS 5.0 chapters. Identity-centric chapters (V6/V7 authentication, V8 authorization) rarely apply to a parsing library; where the crosswalk chapter is not applicable, the closest applicable control (typically consumer-side) is cited instead. DoS and Repudiation are thinly covered by ASVS and are cross-linked to application/infrastructure-layer controls.

### S - Spoofing

| ID | Threat | Attack Path | Likelihood | Impact | Score | Control | Mitigation / Status |
|---|---|---|---|---|---|---|---|
| S-1 | Header spoofing via crafted CSV | Attacker crafts CSV headers that mimic legitimate column names (e.g., "Amount" with unicode homoglyphs); the library does not normalize unicode in header names. | 1 | 2 | 2 | ASVS V2 Validation (consumer) | Accepted - out of scope for a parsing library; consumers should validate headers. |
| S-2 | Auto-renamed headers obscure duplicates | With `AutoRenameHeaders = true` (default), duplicate headers are silently renamed ("A", "A2", "A3"). A malicious CSV could include duplicate headers to cause the application to read from an unexpected column. | 2 | 2 | 4 | ASVS V2 Validation | Mitigated - `AutoRenameHeaders` can be set to `false` to throw on duplicates. |

### T - Tampering

| ID | Threat | Attack Path | Likelihood | Impact | Score | Control | Mitigation / Status |
|---|---|---|---|---|---|---|---|
| T-1 | Separator auto-detection manipulation | When `Separator` is `'\0'` (default), the library auto-detects from the header row by scanning for `;` or `\t`, falling back to `,`. A crafted first row (e.g., `"Name;Email,Phone"`) forces an unintended separator, causing fields to be split or merged incorrectly. | 2 | 2 | 4 | ASVS V2 Validation | Partially mitigated - callers can explicitly set `Separator` to avoid auto-detection. |
| T-2 | Multiline field injection | With `AllowNewLineInEnclosedFieldValues` enabled, crafted fields spanning many lines can alter how subsequent rows are parsed; a maliciously unterminated quoted field consumes the rest of the input. The engine breaks on EOF, limiting blast radius. | 2 | 2 | 4 | ASVS V2 Validation | Partially mitigated - feature is opt-in (`false` by default). No configurable line limit exists for multiline fields (see D-1). |
| T-3 | Quote escape confusion | Multiple quote escape mechanisms are supported (`""`, `\"`, single quotes). With `AllowBackSlashToEscapeQuote` enabled alongside standard double-quote escaping, ambiguous input like `"value\"",next` could parse differently than expected. | 2 | 2 | 4 | ASVS V1 Encoding/Sanitization | Partially mitigated - `AllowBackSlashToEscapeQuote` is opt-in. |
| T-4 | Writer output injection via bare `\r` | Previously, only `\n` was in the string-based writer's quote-trigger set; a bare `\r` (without `\n`) was written unquoted, inconsistently with `CsvBufferWriter`. | 1 | 2 | 2 | ASVS V1 Encoding/Sanitization | **Resolved** (2026) - fixed by [#128](https://github.com/stevehansen/csv/pull/128): `\r` is now in the quote-trigger set (`QuoteTriggerChars`) of both `CsvWriter` (all target frameworks) and `CsvBufferWriter`, so fields containing a bare carriage return are quoted consistently. |
| T-5 | Stale separator via `CsvOptions` reuse | Reusing one `CsvOptions` instance across multiple reads is forbidden but only guarded by a `Debug.Assert` (compiled out in release builds). Auto-detection mutates `options.Separator` in place, so the separator detected from a first (possibly attacker-supplied) document is silently applied to all subsequent documents parsed with the same options instance, changing how they are split. | 2 | 2 | 4 | ASVS V2 Validation | Partially mitigated - documented (`CsvOptions` remarks: "Do not reuse an instance"); debug builds assert. No release-mode guard; callers should create a fresh `CsvOptions` per read and/or set `Separator` explicitly. |

### R - Repudiation

| ID | Threat | Attack Path | Likelihood | Impact | Score | Control | Mitigation / Status |
|---|---|---|---|---|---|---|---|
| R-1 | No parsing audit trail | The library emits no logs or diagnostics about parsing decisions (separator auto-detection result, skipped rows, multiline concatenation). An application cannot audit how input was interpreted. | 1 | 1 | 1 | ASVS V16 Security Logging (application layer) | Accepted - logging is the responsibility of the consuming application. Libraries should not impose a logging framework. |

### I - Information Disclosure

| ID | Threat | Attack Path | Likelihood | Impact | Score | Control | Mitigation / Status |
|---|---|---|---|---|---|---|---|
| I-1 | Pooled buffer data leakage | `CsvMemoryOptions.ClearBuffers` defaults to `false`. Buffers returned to `ArrayPool<char>.Shared` without clearing retain residual data from previous parsing operations; a subsequent consumer of the same pooled buffer (potentially in a different security context within the same process) could observe leftover data. | 2 | 3 | 6 | ASVS V14 Data Protection | Partially mitigated - `ClearBuffers` option exists but defaults to `false` for performance. |
| I-2 | Error messages reveal header names | Exception messages include header names and column counts (e.g., `"Header 'X' does not exist. Expected one of A; B; C"`). In a multi-tenant application, this could leak schema information if exceptions are surfaced. | 2 | 2 | 4 | ASVS V13 Configuration (error handling, consumer) | Accepted - standard .NET exception behavior; applications should catch and sanitize exceptions. |
| I-3 | `ICsvLine.Raw` exposes original input | The `Raw` property retains the original unparsed line. If parsed data is passed downstream, the raw representation (which may include fields the consumer should not see) travels with it. | 1 | 1 | 1 | ASVS V14 Data Protection (consumer) | Accepted - by design for debugging and audit purposes. |

### D - Denial of Service

| ID | Threat | Attack Path | Likelihood | Impact | Score | Control | Mitigation / Status |
|---|---|---|---|---|---|---|---|
| D-1 | Unbounded multiline field concatenation | With `AllowNewLineInEnclosedFieldValues = true`, a single unterminated opening quote causes the engine to concatenate every subsequent line (`source.Concat(line, ...)`) and re-split the growing record on each iteration (`CsvReader.Engine.cs` multiline loops), producing O(n²) memory and CPU. The engine only stops at EOF; there is no limit on lines or total field size. | 3 | 3 | **9** | Infra: input size limits at ingress (ASVS DoS coverage thin); recommended library-level cap | Open - no configurable max field size or max lines per field. Survived the unified-engine refactor (#118/#121); the split-cache priming (#120/#125) avoids one redundant final split but does not bound the blowup. |
| D-2 | Large column count from crafted input | A single line with thousands of separators creates a large `List<MemoryText>` and corresponding header/value arrays. With `ValidateColumnCount` off (default), each row independently allocates arrays. | 2 | 3 | 6 | Infra: input size limits; ASVS V2 Validation | Partially mitigated - `ValidateColumnCount` can catch mismatched rows but not the initial explosion. |
| D-3 | `SkipRow` delegate on every row | The `SkipRow` delegate is invoked for every row; expensive caller-supplied logic slows parsing. Caller-controlled, not attacker-controlled (unless the delegate depends on input). | 1 | 2 | 2 | N/A (caller code) | Accepted - caller responsibility. |
| D-4 | `CsvBufferWriter` unbounded growth | `EnsureCapacity` caps each individual buffer at `MaxBufferSize`, but the `_buffers` list is unbounded; writing very large CSV data consumes memory without limit. | 2 | 3 | 6 | Infra: output size limits (ASVS DoS coverage thin) | Open - no total memory limit across all buffers. |
| D-5 | Regex-free but O(n) per-field scanning | Regex was eliminated in favor of manual scanning, removing regex-based ReDoS. `IsUnterminatedQuotedValue` scans the full field length - linear, not exponential. | 1 | 2 | 2 | ASVS V2 Validation | Mitigated - linear complexity is acceptable. |

### E - Elevation of Privilege

| ID | Threat | Attack Path | Likelihood | Impact | Score | Control | Mitigation / Status |
|---|---|---|---|---|---|---|---|
| E-1 | Formula injection in output (CSV injection) | `CsvWriter` does not sanitize cell values beginning with `=`, `+`, `-`, `@`, `\t`, or `\r`. When output is opened in spreadsheet software (Excel, Google Sheets, LibreOffice Calc), these prefixes can trigger formula execution, potentially leading to data exfiltration or command execution on the user's machine. | 2 | 3 | 6 | ASVS V1 Encoding/Sanitization (output encoding for downstream interpreter, typically consumer) | Open - the library does not sanitize formula-triggering characters. Known industry-wide CSV issue, typically addressed at the application layer. |
| E-2 | `SkipRow` delegate injection | `SkipRow` accepts an arbitrary `Func<>`. A misconfigured application allowing user-controlled code to set this delegate could introduce arbitrary code execution during parsing. | 1 | 3 | 3 | ASVS V2 Validation (caller code integrity) | Accepted - standard delegate pattern; callers control what code they pass. |

---

## Risk Summary

### High Priority Threats (score >= 8)

| ID | Threat | Score | Status |
|---|---|---|---|
| D-1 | Unbounded multiline field concatenation | 9 | Open - no max field size / max lines per field |

### Residual Risks

| Score band | Count | IDs |
|---|---|---|
| High (>= 8) | 1 | D-1 |
| Medium (5-7) | 4 | I-1, D-2, D-4, E-1 |
| Low (2-4) | 10 | S-1, S-2, T-1, T-2, T-3, T-5, I-2, D-3, D-5, E-2 |
| Informational (1) | 2 | R-1, I-3 |
| Resolved | 1 | T-4 |

---

## Security Controls Summary

| Control category | Implementation |
|---|---|
| Output encoding | RFC 4180 quoting in `CsvWriter` and `CsvBufferWriter`: fields containing quotes, the separator, `'`, `\n`, or `\r` are quoted; embedded quotes doubled. Writers aligned on bare-`\r` handling by #128 (T-4). |
| Input validation | `ValidateColumnCount` (opt-in), `AutoRenameHeaders = false` strict duplicate detection (opt-in), explicit `Separator` to bypass auto-detection. |
| Memory hygiene | `CsvMemoryOptions.ClearBuffers` (opt-in) clears pooled buffers; `MaxBufferSize` caps individual buffer size. |
| DoS resistance | Regex-free linear scanning (no ReDoS); multiline fields opt-in. Gap: no total field-size / line-count cap (D-1) and no total writer memory cap (D-4). |
| Safe defaults | Multiline fields, backslash escapes, and single-quote enclosure all default off; enclosed values default on per RFC 4180. |

---

## Recommendations

### For Library Maintainers

1. **D-1**: Consider adding a configurable maximum field size or maximum lines per multiline field to `CsvOptions` (e.g., `MaxFieldLength`, `MaxLinesPerField`). This would protect against quadratic blowup from unterminated quotes.
2. **I-1**: Consider documenting the security implications of `ClearBuffers = false` more prominently, or changing the default to `true` for security-sensitive scenarios.
3. **D-4**: Consider adding a total memory limit to `CsvBufferWriter` (sum of all buffer sizes) and throwing when exceeded.
4. **T-5**: Consider a release-mode guard (throw, not assert) when a `CsvOptions` instance is reused across enumerations, since reuse silently carries the previously detected separator.

### For Library Consumers

1. **Always set `Separator` explicitly** when parsing untrusted input to avoid auto-detection manipulation (T-1) and stale-separator carryover (T-5).
2. **Create a fresh `CsvOptions` per read** - instances must not be reused across enumerations (T-5).
3. **Use `ValidateColumnCount = true`** when the expected schema is known to detect malformed rows early (D-2).
4. **Set `AllowNewLineInEnclosedFieldValues` to `true` only when needed**, and be aware of the DoS risk with unterminated quotes (D-1).
5. **Sanitize cell values before writing** if the output CSV may be opened in spreadsheet software. Prefix cells starting with `=`, `+`, `-`, `@`, `\t`, `\r` with a single quote or tab character (E-1).
6. **Set `ClearBuffers = true`** in `CsvMemoryOptions` when processing sensitive data in multi-tenant or shared-memory scenarios (I-1).
7. **Catch and sanitize exceptions** before surfacing them to end users to avoid leaking header/schema information (I-2).
8. **Set `AutoRenameHeaders = false`** when strict header validation is required to detect duplicate headers as errors (S-2).

---

## Review History

| Version | Date | Baseline | Changes |
|---|---|---|---|
| v1 | 2026-02-20 | `80356b8` | Initial threat model (17 threats). |
| v2 | 2026-07-07 | `b04efcc` | T-4 (bare `\r` writer inconsistency) resolved by #128. Added T-5 (stale separator via `CsvOptions` reuse - engine guards reuse with `Debug.Assert` only). Re-validated all threats against the unified read engine (#118/#121) and `CsvLine<TPolicy>` consolidation (#132/#133): D-1, D-4, I-1, T-1, T-2, T-3, E-1 confirmed still present. Backfilled Likelihood x Impact scoring and ASVS 5.0 control citations for every threat; added Risk Summary bands, Security Controls Summary, Review History, and References sections. |

---

## References

- [Microsoft STRIDE threat modeling](https://learn.microsoft.com/en-us/azure/security/develop/threat-modeling-tool-threats)
- [OWASP Application Security Verification Standard (ASVS) 5.0](https://owasp.org/www-project-application-security-verification-standard/)
- [RFC 4180 - Common Format and MIME Type for CSV Files](https://www.rfc-editor.org/rfc/rfc4180)
- [OWASP: CSV Injection](https://owasp.org/www-community/attacks/CSV_Injection)
