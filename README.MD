# This is a POC for improving FSharp coding with data oriented software engineering using SOTA models

## Usage
- Insert your anthropic key
- Install the requirements with `pip install -r requirements.txt`
- python main.py <path_to_fsharp_project> <fsharp_file>
- Be ready to go broke and burn your API Credits

There is a sample fsharp_project in the `fsharp_project` folder.

## How does it work
- We take the fsharp project with BenchmarkDotNet
- Run it in Docker and parse BenchmarkDotNet results (execution time only for now)
- Make an improvement
- Run the benchmark again (At the moment if the code fails to compile, we skip the improvement)
- If the improvement is better than the previous one, we keep it and repeat the process

## Results after 10 improvement rounds
The response times show a decreasing trend from 126.9ms → 110.0ms → 107.7ms, indicating improving performance.
I stopped after 10 rounds because I was running out of API credits (cost me 20$).

<svg width="400" height="300" xmlns="http://www.w3.org/2000/svg">
  <!-- Background -->
  <rect width="400" height="300" fill="#f8f9fa"/>
  <!-- Grid lines -->
  <g stroke="#e0e0e0" stroke-width="1">
    <line x1="50" y1="50" x2="350" y2="50"/>
    <line x1="50" y1="100" x2="350" y2="100"/>
    <line x1="50" y1="150" x2="350" y2="150"/>
    <line x1="50" y1="200" x2="350" y2="200"/>
    <line x1="50" y1="250" x2="350" y2="250"/>
  </g>
  <!-- Axes -->
  <g stroke="#333" stroke-width="2">
    <line x1="50" y1="50" x2="50" y2="250"/>
    <line x1="50" y1="250" x2="350" y2="250"/>
  </g>
  <!-- Y-axis labels -->
  <g fill="#333" font-family="Arial, sans-serif" font-size="12">
    <text x="10" y="55" text-anchor="middle">130</text>
    <text x="10" y="105" text-anchor="middle">120</text>
    <text x="10" y="155" text-anchor="middle">110</text>
    <text x="10" y="205" text-anchor="middle">100</text>
    <text x="10" y="255" text-anchor="middle">90</text>
  </g>
  <!-- X-axis labels -->
  <g fill="#333" font-family="Arial, sans-serif" font-size="12">
    <text x="100" y="270" text-anchor="middle">1</text>
    <text x="200" y="270" text-anchor="middle">2</text>
    <text x="300" y="270" text-anchor="middle">3</text>
  </g>
  <!-- Data points and line -->
  <polyline points="100,73 200,150 300,172" stroke="#4285f4" stroke-width="2" fill="none"/>
  <!-- Data point circles -->
  <g fill="#4285f4" stroke="#fff" stroke-width="2">
    <circle cx="100" cy="73" r="5"/>
    <circle cx="200" cy="150" r="5"/>
    <circle cx="300" cy="172" r="5"/>
  </g>
  <!-- Value labels -->
  <g fill="#333" font-family="Arial, sans-serif" font-size="11" font-weight="bold">
    <text x="100" y="65" text-anchor="middle">126.9</text>
    <text x="200" y="142" text-anchor="middle">110.0</text>
    <text x="300" y="164" text-anchor="middle">107.7</text>
  </g>
  <!-- Title -->
<text x="200" y="30" text-anchor="middle" font-family="Arial, sans-serif" font-size="16" font-weight="bold" fill="#333">Execution Time (ms)</text>
  <!-- X-axis title -->
<text x="200" y="290" text-anchor="middle" font-family="Arial, sans-serif" font-size="12" fill="#333">Measurement #</text>
</svg>


## F# Performance Optimization Checklist

### 📊 Data Structure Selection

#### Collections Performance Hierarchy (Fastest to Slowest)
- [ ] **Arrays** - Use for random access and numerical operations
- [ ] **Dictionary<K,V>** - Use for lookups (15ms/100k ops)
- [ ] **HashSet<T>** - Use for unique item collections
- [ ] **ResizeArray<T>** - Use when size varies significantly
- [ ] **Map<K,V>** - Avoid in hot paths (10x slower than Dictionary)
- [ ] **List<T>** - Avoid for performance-critical code (100x slower for search)

#### Collection Processing
- [ ] Replace `List.map` with `Array.map` in hot paths
- [ ] Use `Array.Parallel` for CPU-intensive operations on large datasets
- [ ] Prefer `Array.filter` over `List.filter` (20% faster after optimizations)
- [ ] Use `Array.partition` for dual filtering (30% less memory)
- [ ] Consider `Seq` for lazy evaluation of large datasets

### 🌿 Branch Prediction Optimization

#### Pattern Matching
- [ ] Order match cases by frequency (most common first)
- [ ] Avoid guards (`when` clauses) in hot paths
- [ ] Replace active patterns with direct matching in performance code
- [ ] Consider lookup tables for complex branching logic

#### Branchless Techniques
- [ ] Convert if/then/else to arithmetic operations where possible
- [ ] Use conditional moves: `condition * trueValue + (1 - condition) * falseValue`
- [ ] Replace min/max conditions with `Math.Min`/`Math.Max`
- [ ] Eliminate null checks with null-safe operators

### 💾 Memory Optimization

#### Struct Usage
- [ ] Convert small records (≤16 bytes) to structs
- [ ] Add `[<Struct>]` attribute to eligible types
- [ ] Include `[<IsReadOnly>]` to prevent defensive copying
- [ ] Verify struct size with `sizeof<'T>`
- [ ] Avoid structs for types with reference fields

#### Memory Pooling
- [ ] Implement `ArrayPool<T>.Shared` for temporary buffers
- [ ] Always return buffers in `finally` blocks
- [ ] Use `MemoryPool<T>` for sliceable memory segments
- [ ] Clear sensitive data before returning to pool

#### Span and Memory
- [ ] Replace array slicing with `Span<T>` (25x improvement)
- [ ] Use `ReadOnlySpan<T>` for immutable views
- [ ] Convert string operations to `ReadOnlySpan<char>`
- [ ] Leverage `stackalloc` for small temporary buffers

#### Allocation Reduction
- [ ] Eliminate intermediate array allocations in pipelines
- [ ] Cache delegate instances to avoid closure allocations
- [ ] Use `ValueOption` instead of `Option` in hot paths
- [ ] Avoid boxing by using concrete types in generic functions


### 🔧 Code-Level Optimizations

#### Loop Optimization
- [ ] Unroll small, fixed-size loops
- [ ] Hoist invariant calculations outside loops
- [ ] Use `for` loops instead of `Seq.iter` for arrays
- [ ] Minimize allocations inside loops

#### String Handling
- [ ] Replace string concatenation with `StringBuilder`
- [ ] Use `String.Create` for known-size strings
- [ ] Cache frequently-used string values
- [ ] Consider interning for repeated string literals

#### Numeric Optimization
- [ ] Use primitive types directly (avoid generic math where possible)
- [ ] Replace `pown` with specific power operations
- [ ] Leverage SIMD operations via `System.Numerics.Vectors`
- [ ] Use bit manipulation for power-of-2 operations

### ⚡ Architecture-Level Optimizations

#### Caching Strategies
- [ ] Implement memoization for pure functions
- [ ] Use `ConcurrentDictionary` for thread-safe caching
- [ ] Apply time-based or size-based cache eviction
- [ ] Consider distributed caching for scale-out scenarios

#### Parallelization
- [ ] Identify embarrassingly parallel operations
- [ ] Use `Array.Parallel` for data parallelism
- [ ] Implement `Async.Parallel` for I/O parallelism
- [ ] Consider TPL Dataflow for pipeline parallelism

#### I/O Optimization
- [ ] Use async/await for all I/O operations
- [ ] Implement buffering for file/network operations
- [ ] Batch database queries
- [ ] Use streaming for large data processing
