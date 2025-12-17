## 2025-12-16 - List Allocation in CSV Splitting
**Learning:** `CsvReader` was allocating a new `List<T>` for every line without specifying capacity, leading to multiple array resizes per line. Pre-allocating using `headers.Length` as a hint (since CSVs are typically rectangular) provided a ~13% performance boost in a simple benchmark. Also, `SplitLineOptimized` was re-instantiating `CsvLineSplitter` unnecessarily.
**Action:** Always check loop-heavy allocations (like `new List()`) and see if a size hint is available. Verify object reuse in "optimized" paths.
