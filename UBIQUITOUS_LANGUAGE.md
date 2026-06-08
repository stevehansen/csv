# Ubiquitous Language

The shared vocabulary of the `Csv` library — what a CSV *is*, and how we read, write, and
configure it. Canonical terms are **bold**. The "Aliases to avoid" column lists words the
codebase currently uses for the same concept that we should stop using.

> The single most important distinction in this codebase: a **physical line** (newline-bounded
> source text) is *not* the same as a **record** (one logical CSV entry). A record with a
> multiline field spans several physical lines. The public reading interface is named
> `ICsvLine` but actually represents a **record** — see *Flagged ambiguities*.

## Document structure

| Term               | Definition                                                                                                                          | Aliases to avoid              |
| ------------------ | ----------------------------------------------------------------------------------------------------------------------------------- | ----------------------------- |
| **Record**         | One complete CSV entry: a sequence of fields forming a single logical row; may span several physical lines if it has a multiline field. | row, line, data line, data row |
| **Physical line**  | A run of source characters terminated by `\n` or `\r\n`. Usually one record = one physical line, but a multiline field breaks that 1:1. | line (when a record is meant) |
| **Field**          | A single datum within a record, delimited by separators and optionally enclosed in quotes.                                          | cell, part, data              |
| **Value**          | The parsed string content of a field — after trimming and unescaping. (A field is the slot; the value is what's in it.)             | —                             |
| **Separator**      | The single character that delimits fields within a record (comma by default; auto-detected when unset).                             | delimiter                     |
| **Column**         | The vertical slot occupying the same position across all records; named by a header, located by a column index.                     | (see Header / Column index / Field count) |

## Headers & columns

| Term             | Definition                                                                                                          | Aliases to avoid          |
| ---------------- | ------------------------------------------------------------------------------------------------------------------- | ------------------------- |
| **Header**       | The *name* of a column, taken from the header row (`HeaderPresent`) or auto-generated as `Column1`, `Column2`, … (`HeaderAbsent`). | column name (when ambiguous) |
| **Header row**   | The first non-skipped record, interpreted as the set of headers.                                                    | first line                |
| **Header mode**  | Whether the input carries a header row: `HeaderPresent` (default) or `HeaderAbsent`.                                 | —                         |
| **Column index** | The zero-based position of a field within a record.                                                                 | column (when a position is meant) |
| **Field count**  | The number of fields in a record. This is what `ICsvLine.ColumnCount` and `ValidateColumnCount` actually measure.   | column count              |
| **Alias**        | An alternative header that resolves to the same column (e.g. `CategoryName` / `Category Name` / `Category-Name`).    | —                         |
| **Auto-rename**  | Making duplicate or empty headers unique by appending a number (`A`, `A2`, …; `Empty`, `Empty2`, …).                | —                         |
| **Comparer**     | The string-equality rule used when matching a header name (case-sensitive or case-insensitive).                     | —                         |

## Quoting, escaping & multiline

| Term                          | Definition                                                                                                                  | Aliases to avoid                  |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------- | --------------------------------- |
| **Quote character**           | The character that opens and closes an enclosed field — `"` by default, `'` when `AllowSingleQuoteToEncloseFieldValues` is set. | enclosure                         |
| **Enclose**                   | To wrap a field in quote characters so it may safely contain separators, quotes, or newlines (`AllowEnclosedFieldValues`).   | quote (as a verb, for wrapping)   |
| **Quote escaping**            | Representing a literal quote inside an enclosed field by doubling it (`""` → one `"`).                                       | escape (bare)                     |
| **Backslash escape**          | Optional alternative to doubling: `\"` represents a literal quote (`AllowBackSlashToEscapeQuote`).                           | —                                 |
| **Multiline field**          | An enclosed field whose value contains literal newlines, making its record span multiple physical lines (`AllowNewLineInEnclosedFieldValues`). | —                                 |
| **Unterminated quoted value** | An enclosed field with no closing quote on the current physical line — the signal that the record continues onto the next.   | open quote                        |
| **Continuation**              | Appending the next physical line(s) to an unterminated quoted value until the closing quote is found, joined with `NewLine`. | —                                 |
| **Quoting** (writing)         | The writer's decision to *enclose* a field because it contains a separator, quote, or newline. Distinct from escaping.       | escape, escaping (the writer's internal name) |

## Reading & writing pipeline

These are the codebase's nouns for the read/write architecture — part of the team's working
vocabulary even though a CSV end-user wouldn't say them.

| Term                | Definition                                                                                                       | Aliases to avoid |
| ------------------- | ---------------------------------------------------------------------------------------------------------------- | ---------------- |
| **Source**          | The input a read consumes: a `TextReader`, `Stream`, `string`, or `ReadOnlyMemory<char>`.                        | —                |
| **Line source**     | The internal abstraction that yields one physical line at a time, hiding which kind of source it came from.       | —                |
| **Engine**          | The single generic read loop driving every read path (sync, async, span, memory).                                | —                |
| **Row factory**     | The internal component that builds a record object from a parsed line.                                            | —                |
| **Raw split line**  | A record's fields immediately after splitting on separators — before any trimming or unescaping.                 | —                |
| **Parsed line**     | A raw split line after trimming and unescaping have produced the final values.                                   | —                |
| **Skip row**        | A predicate that drops a physical line *before* parsing (defaults to dropping blank lines and `#` comment lines). | —                |
| **Rows to skip**    | A fixed count of leading physical lines discarded before the header row is read.                                  | —                |
| **Trim data**       | Option to strip leading/trailing whitespace from values (and to permit a quote to open after leading whitespace). | —                |
| **Validate column count** | Option requiring every record's field count to equal the header count.                                      | —                |
| **Auto-detect separator** | Inferring the separator from the header row when none is configured.                                        | —                |

## Relationships

- A **CSV document** is an optional **header row** followed by zero or more **records**.
- A **record** contains one or more **fields**; each field holds one **value**.
- A **field** is delimited by **separators** and may be **enclosed** in **quote characters**.
- A **column** is the set of same-position fields across records; its name is a **header**, its position a **column index**.
- One or more **aliases** may resolve to the same **column**.
- An **enclosed field** containing newlines is a **multiline field**; its **record** spans multiple **physical lines**, joined during **continuation** once an **unterminated quoted value** is detected.
- Inside an enclosed field, a literal quote is produced by **quote escaping** (`""`) or, if enabled, a **backslash escape** (`\"`).
- **Field count** equals the **header** count when **validate column count** is on.
- On write, a field is **quoted** (enclosed) when it contains a separator, quote, or newline; the literal quotes inside it are then **escaped** by doubling.

## Example dialogue

> **Dev:** "When I read a `HeaderAbsent` file, what does `ICsvLine.ColumnCount` give me?"

> **Domain expert:** "The **field count** of that **record** — how many **fields** it has. It's named 'column count', but there are no **headers** in `HeaderAbsent` mode, so it's really just the number of fields on that line."

> **Dev:** "And if one of those fields is a **multiline field**, do I get several **records**?"

> **Domain expert:** "No — one **record**. The value spans several **physical lines**, but because the **enclosed field** is an **unterminated quoted value** on the first line, the reader does a **continuation**: it keeps appending physical lines until it sees the closing **quote character**, then hands you a single record."

> **Dev:** "Inside that value there's a `\"\"`. Is that two fields?"

> **Domain expert:** "No — that's **quote escaping**: a doubled quote is one literal `\"`. A **separator** only splits fields when you're *outside* an enclosed field."

> **Dev:** "On the writing side, when does a **field** get wrapped in quotes?"

> **Domain expert:** "That's **quoting**, not escaping — the writer **encloses** a field when it contains a separator, a quote, or a newline. The writer's internals confusingly call that 'escape', but escaping proper is only the doubled-quote step that happens *after* it decides to enclose."

## Flagged ambiguities

1. **`line` / `row` / `record` are used interchangeably**, but the code genuinely needs two concepts: a **physical line** (newline-bounded source text) and a **record** (one logical entry, possibly multi-line). The public interface `ICsvLine` is named after "line" yet represents a **record**. *Recommendation:* say **record** for the logical entry and **physical line** for source text; retire "row" and "data line" as a third synonym.

2. **`column` means four different things** — a header *name*, a zero-based *index*, the *vertical slot* across records, and a *count*. The sharpest offenders are `ICsvLine.ColumnCount` and `CsvOptions.ValidateColumnCount`, which actually measure **fields per record**, not columns. *Recommendation:* **Header** (name), **Column index** (position), **Column** (the vertical slot), **Field count** (per-record count).

3. **One datum has five names** — `field`, `value`, `cell`, `data`, `part`. `CsvLineSplitter` comments say "cell" and "part"; the writer says "cell"; `TrimData` and the `ICsvLine` doc-comments say "data"; the array property is `Values`. *Recommendation:* **Field** for the unit, **Value** for its parsed content; retire cell / part / data.

4. **`escape` is overloaded across reading and writing.** It means (a) the per-character **quote escaping** that produces a literal quote (`""`), (b) the optional **backslash escape** (`\"`), and (c) — in the writer — the decision to *wrap a whole field in quotes* (`escapeChars`, `needsGeneralEscape`, `FixedEscapeChars`). The third isn't escaping at all; it's **quoting**. *Recommendation:* reserve **escape** for the doubled-quote/backslash step, and call the wrap-the-field decision **quoting**.

5. **`separator` vs `delimiter`** — the property and implementation use **Separator**; "delimiter" appears only in prose. *Recommendation:* **Separator** everywhere.

6. **`enclose` vs `quote`** — option names say *enclose* (`AllowEnclosedFieldValues`), the splitter says *quote* (`inQuotes`, `quoteChar`). These are *not* synonyms: **enclose** is the act of wrapping; the **quote character** is the tool that does it. *Recommendation:* keep both, used precisely.

## Applying this glossary (blast radius)

`Csv` is a public NuGet package with millions of downloads, so **renaming a public member is a
SemVer-major breaking change**. The recommendations above therefore apply differently depending
on where a name lives:

| Bucket | Rule | Status |
| ------ | ---- | ------ |
| 🟢 **Internal / private** (`internal`, `private`, locals) | Rename freely — internals are exposed only to `Csv.Tests` via `InternalsVisibleTo`, so there is zero ecosystem impact. | **Done** (see below) |
| 🟡 **Public XML-doc text** | Reword to canonical terms — comments are not part of the binary/source contract, so this is non-breaking. | **Done** (see below) |
| 🔴 **Public member names** (types, properties, methods, enum members, method parameters) | Frozen. Cannot change without a major version + migration guide. Method *parameter* names count — named-argument callers depend on them. | **Deferred to vNext** |

### Internal renames applied (non-breaking)

- The reader record classes (`ReadLine`, `ReadLineSpan`, `ReadLineSpanOptimized`, `ReadLineFromMemory`):
  `rawSplitLine` → **`rawFields`**, `RawSplitLine` → **`RawFields`**, `parsedLine` → **`parsedValues`**,
  and the private property literally named `Line` (which returned the parsed field array) → **`ParsedValues`**.
- The writer's escape-vs-quote confusion (`CsvWriter`, `CsvBufferWriter`): `FixedEscapeChars` →
  **`QuoteTriggerChars`**, `escapeChars` → **`quoteTriggerChars`**, `needsGeneralEscape` →
  **`needsQuoting`**, `escape` (the wrap-the-field decision) → **`mustQuote`**. `needsQuoteEscape`
  is kept — it genuinely means quote-doubling.

> The writer deliberately keeps `cell` / `WriteCell` / `WriteRow` / `WriteLine`. Those names are
> baked into the **public** writer API, so the private helpers stay consistent with them rather
> than with the reader's `field`/`record` vocabulary. `cell` is thus an accepted writer-side
> synonym for **field**.

### Doc rewording applied (non-breaking)

`ICsvLine.ColumnCount` / `ICsvLineFromMemory.ColumnCount` now document "number of **fields** in
this record"; `CsvOptions.ValidateColumnCount` documents matching the "**field count** per row";
the `Read*` summaries say "Reads the **records**"; the `int` indexers and `ICsvLineSpan`'s
`GetSpan`/`GetMemory`/`TryGet*` document a "**field index**" instead of a "column index";
`CsvOptions.Aliases` maps names to a "**header/column**"; `CsvBufferWriter.WriteCell` documents
"**quoting and escaping**"; and the splitter is documented as splitting a record's text into
**fields**.

### vNext rename targets (require a major version)

When a `v3` is on the table, these public names are the ones worth correcting, each behind an
`[Obsolete]` forwarder so the old name keeps working through one major version:

| Today (frozen) | Canonical target | Why |
| -------------- | ---------------- | --- |
| `ICsvLine.ColumnCount` | `FieldCount` | It counts fields in a record, not columns. |
| `CsvOptions.ValidateColumnCount` | `ValidateFieldCount` | Same — it validates field count per record. |
| `ICsvLine.LineHasColumn(name)` | `RecordHasValue(name)` | "Line" means record; it tests for a present value. |
| `ICsvLine` / `ICsvLineSpan` / `ICsvLineFromMemory` | `ICsvRecord` / `ICsvRecordSpan` / … | The type models a record, not a physical line. |

Smaller doc-vs-name frictions (e.g. `ReturnEmptyForMissingColumn`, the writer's `WriteCell`) are
**not** worth a breaking change — leave them and lean on the docs.

> **Not recommended:** `[Obsolete]` on the *current* names now (it would emit build warnings for
> every consumer over a naming preference), or additive aliases like a second `FieldCount` next to
> `ColumnCount` (two ways to do one thing is its own consistency tax). Reworded docs already carry
> the canonical meaning at zero cost.

---

*Out of scope:* `CsvMemoryOptions` knobs (`ReuseBuffers`, `InitialBufferSize`, `MaxBufferSize`,
`DirectAllocationThreshold`, `EnableZeroCopy`, `UseVectorization`, `ClearBuffers`,
`StreamingThreshold`) and buffer/span/pool plumbing are .NET performance-tuning vocabulary, not
CSV-domain language, and are deliberately excluded from this glossary.
