# Architecture Deepening Candidates

> Output of an `improve-codebase-architecture` exploration pass (John Ousterhout,
> *A Philosophy of Software Design* — deep modules: small interface hiding a large
> implementation; shallow modules leak complexity to callers).
>
> Date: 2026-06-20. Branch: `master`.

## Binding constraint: the public API is frozen

`ICsvLine`, `ICsvLineSpan`, `ICsvLineFromMemory` and every `CsvReader` / `CsvWriter`
static method are fixed. **Every candidate below is an internal-only refactor** — the
row classes are `internal sealed`, the parsing engine is `private`, `CsvLineSplitter`
is `internal`. All deepenings are safe under the freeze.

All four candidates are **dependency category: In-process** (pure computation; the only
I/O is already hidden behind `ILineSource` / `IAsyncLineSource`). So each is directly
mergeable and testable at the boundary with no test stand-ins.

---

## Candidate 1 — Collapse the four near-identical row classes

**Cluster:** `ReadLine` (`CsvReader.cs:448-545`), `ReadLineSpan` (`549-688`),
`ReadLineSpanOptimized` (`690-832`), `ReadLineFromMemory` (`FromMemory.cs:22-107`).
~465 lines.

**Why coupled:** All four own *the same concept* — "a parsed row: lazy `RawFields` →
validate → `ParsedValues` → access by name/index." They differ only in backing store
(`string` Raw vs `ReadOnlyMemory<char>` raw) and which split helper they call.
`ReadLineSpan` and `ReadLineSpanOptimized` are ~140 lines each and differ almost solely
in storage. Validation, the `ReturnEmptyForMissingColumn` branch, and **three
error-message string literals** are copy-pasted 3–4× each.

**Test impact:** `EngineUnificationTests.cs` (675 lines, 20 tests) exists *only* to prove
the five read paths emit identical `Headers`/`Values`/`Raw`; `IssuesTests` #114 re-tests
one scenario across 3 paths. Those collapse to one row-boundary suite + thin per-path
wiring tests. No test constructs a row class directly today.

**Note:** Flagged in project memory as *"four near-identical row classes persist;
refactoring deferred."* Highest duplication, lowest risk.

---

## Candidate 2 — Extract a single Field Codec (escape ⇄ unescape ⇄ quote-decision)

**Cluster:** Read side — `Trim` (`CsvReader.cs:346`), `TrimOptimized` (`840`),
`StringHelpers.Unescape`, `CsvLineSplitter` (`Split` + `IsUnterminatedQuotedValue` +
`IsAtFieldOpen`). Write side — **five** independent "decide-if-quote + double-the-quotes"
implementations: `WriteLine` (`CsvWriter.cs:484`, via `.Replace`), `WriteLineAsync`
(`528`, manual segments), `WriteCellMemory` (`623`), `WriteCellToBuffer` (`682`),
`CsvBufferWriter.WriteCell` (`104`).

**Why coupled:** One contract — *"what makes a field need quoting, and how are quotes
escaped"* — expressed **seven times, in two opposite directions, sharing zero code.** The
quote-trigger set (`"`, separator, `\n`, `\r`, `'`) is re-typed in all five writers; the
inverse unescape rules live scattered across reader `Trim`/`TrimOptimized`/splitter.
Reader and writer can silently drift out of round-trip symmetry.

**Test impact:** `RegexEliminationTests` (9) + `RegexPerformanceTests` (2) directly test
the internal `IsUnterminatedQuotedValue`; writer escaping is tested *only* indirectly via
`WriterTests` (41). A codec gets exhaustive direct boundary tests — including
`unescape(escape(x)) == x`, which no test asserts today.

**Note:** Deepest, most cross-cutting, highest payoff — also the largest surface. The
write-only sub-slice (collapse the 5 writer impls) is a viable smaller scope.

---

## Candidate 3 — Unify the sync/async parsing engine

**Cluster:** `Enumerate` (`CsvReader.Engine.cs:246-344`) and `EnumerateAsync`
(`347-451`). ~99 lines each.

**Why coupled:** Byte-for-byte the same orchestration — `RowsToSkip`/`SkipRow`,
`InitializeOptions`, the HeaderAbsent multiline pre-pass, `GetHeaders` vs
`CreateDefaultHeaders`, `CreateHeaderLookup` + the duplicate-header catch, alias
resolution, and the multiline-continuation loop — duplicated verbatim. The *only* real
difference is one line: `source.TryReadLine(...)` vs `await source.TryReadLineAsync(...)`.

**Test impact:** `EngineUnificationTests` already asserts sync/async parity — dedup makes
that parity *structural* rather than test-enforced.

**Note:** Highest-value logic to unify, but the **hardest in C#** — sync/async can't share
a body without a pull-model state machine or sync-over-async. A genuine "design it twice"
case.

---

## Candidate 4 — Retire the `options.Splitter` mutable side-channel

**Cluster:** `CsvOptions.Splitter` (`CsvOptions.cs:103`, internal mutable on a public
class), `InitializeOptions` (`CsvReader.cs:333`), and the
`Debug.Assert(options.Splitter == null)` reuse-detector in `Enumerate`/`EnumerateAsync`.

**Why coupled:** The splitter is stitched onto the options object as hidden mutable state,
then "don't reuse options" is enforced via a Debug-only assert — a *"define errors out of
existence"* violation. `CsvLineSplitter` itself is shallow (holds only `separator`,
re-reads everything else from `options` per call; `IsUnterminatedQuotedValue` is static).

**Test impact:** Minimal — correctness/clarity cleanup that removes a footgun.

**Note:** Smallest scope. Largely subsumed by Candidate 2 (the splitter is the read-side
of the codec). Best as a rider on #2.

---

## How they relate

- **#1** (row data-access shape) and **#2** (field byte-level semantics) are the two big
  wins and are largely independent.
- **#3** is high-value but design-hard.
- **#4** folds into #2.

**Recommendation:** **#1 is the cleanest first move** (explicitly-deferred, lowest risk,
collapses the largest test-duplication). **#2 is the deepest** (true reader/writer
symmetry; write-side currently untested in isolation).

---

> **Note:** Candidate 3 (engine unification) was already shipped as #118 — that RFC's
> `Enumerate<TSource, TFactory, TRow>` is why `CsvReader.Engine.cs` exists. Candidate 1 is
> its row-side sequel.

---

## Selected for deep exploration: **Candidate 1** → RFC [#132](https://github.com/stevehansen/csv/issues/132)

A parallel exploration produced **four divergent interface designs**, each adversarially
judged against the four hard constraints (API-freeze, multi-target, allocation parity, AOT):

| Design | Shape | ns2.0 | alloc | score |
|---|---|:--:|:--:|:--:|
| 1. minimal-surface | `CsvRow<TStore,TBacking>` (2 type params), all 3 interfaces | ✗ `null` | ✓ | 7 |
| 2. max-flexibility | core + 3 view-wrapper classes (open/closed) | ✗ | ✓⁻ (extra obj/row) | 5 |
| 3. common-caller-first | two-tier: `ReadLine` as-is + memory `CsvRow<TStrategy>` | ✓ | ✗ (`Raw` realloc) | 7 |
| 4. zero-alloc-first | one `CsvRow<TPolicy>`, `MemoryText`-backed, union-of-interfaces | ✗ `null` | ✓ | 6 |

**Cross-cutting findings (all four judges confirmed):** one class implementing
`ICsvLineSpan` **and** `ICsvLineFromMemory` is legal via *explicit* interface implementation
(indexers differ only by return type); `IEnumerable<out T>` covariance returns one closed
generic as any element type without a wrapper; the struct-generic strategy preserves JIT
devirtualization. **The constraint that broke 3 of 4:** `return default!` is `null` on
netstandard2.0, silently regressing `ReturnEmptyForMissingColumn` from `""` to `null`.

**Chosen: hybrid — #4 spine + the discipline #3 got right.** Single generic
`CsvLine<TPolicy>`, hardened with three corrections (empty-value `"".AsMemory()`,
internal `parsedValues` + `GetBlock` initializer, cached original `rawString` for
zero-alloc `Raw`). Full duplication collapse (~465 → ~210 LOC), all constraints met.
See [#132](https://github.com/stevehansen/csv/issues/132) for the full RFC.
