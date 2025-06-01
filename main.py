from strands import Agent
from strands.models.anthropic import AnthropicModel
from fsharp_runner import FSharpBenchmark
from fsharp_file_handler import read_file, create_project, write_file

if __name__ == "__main__":
    import sys
    if len(sys.argv) < 3:
        print("Usage: python main.py <path_to_fsharp_project> <fsharp_file>")
        sys.exit(1)
    fsharp_project_path = sys.argv[1]
    fsharp_file = sys.argv[2]

    generator_model = AnthropicModel(
        client_args={
            "api_key": "insert API Key here",
        },
        # **model_config
        max_tokens= (4096 * 2),
        model_id="claude-opus-4-20250514",
        params={
            "temperature": 0.2,
        }
    )
    coding_agent = Agent(
        system_prompt="You are a professional F# developer with expertise in performance optimization."
                      "Your task is to analyze and improve the performance of F# code based on the provided checklist."
                      "Be concise and focus on practical improvements.",
        model=generator_model
    )

    improvement_checklist = {
                "Data Structure Selection - Collections": [
                    "Arrays - Use for random access and numerical operations",
                    "Dictionary<K,V> - Use for lookups (15ms/100k ops)",
                    "HashSet<T> - Use for unique item collections",
                    "ResizeArray<T> - Use when size varies significantly",
                    "Map<K,V> - Avoid in hot paths (10x slower than Dictionary)",
                    "List<T> - Avoid for performance-critical code (100x slower for search)"
                ],
                "Data Structure Selection - Processing": [
                    "Replace List.map with Array.map in hot paths",
                    "Use Array.Parallel for CPU-intensive operations on large datasets",
                    "Prefer Array.filter over List.filter (20% faster after optimizations)",
                    "Use Array.partition for dual filtering (30% less memory)",
                    "Consider Seq for lazy evaluation of large datasets"
                ],
                "Branch Prediction - Pattern Matching": [
                    "Order match cases by frequency (most common first)",
                    "Avoid guards (when clauses) in hot paths",
                    "Replace active patterns with direct matching in performance code",
                    "Consider lookup tables for complex branching logic"
                ],

                "Branch Prediction - Branchless Techniques": [
                    "Convert if/then/else to arithmetic operations where possible",
                    "Use conditional moves: condition * trueValue + (1 - condition) * falseValue",
                    "Replace min/max conditions with Math.Min/Math.Max",
                    "Eliminate null checks with null-safe operators"
                ],


                "Memory Optimization - Struct Usage": [
                    "Convert small records (≤16 bytes) to structs",
                    "Add [<Struct>] attribute to eligible types",
                    "Include [<IsReadOnly>] to prevent defensive copying",
                    "Verify struct size with sizeof<'T>",
                    "Avoid structs for types with reference fields"
                ],

                "Memory Optimization - Memory Pooling": [
                    "Implement ArrayPool<T>.Shared for temporary buffers",
                    "Always return buffers in finally blocks",
                    "Use MemoryPool<T> for sliceable memory segments",
                    "Clear sensitive data before returning to pool"
                ],

                "Memory Optimization - Span and Memory": [
                    "Replace array slicing with Span<T> (25x improvement)",
                    "Use ReadOnlySpan<T> for immutable views",
                    "Convert string operations to ReadOnlySpan<char>",
                    "Leverage stackalloc for small temporary buffers"
                ],

                "Memory Optimization - Allocation Reduction": [
                    "Eliminate intermediate array allocations in pipelines",
                    "Cache delegate instances to avoid closure allocations",
                    "Use ValueOption instead of Option in hot paths",
                    "Avoid boxing by using concrete types in generic functions"
                ],



                "Compiler Optimization - Inline Annotations": [
                    "Add inline to small, frequently-called functions",
                    "Inline generic comparison/equality operators",
                    "Mark mathematical operators as inline",
                    "Verify inlining with IL inspection tools"
                ],

                "Compiler Optimization - Tail Call": [
                    "Structure recursive functions for tail calls",
                    "Use accumulator parameters",
                    "Avoid operations after recursive calls",
                    "Verify tail call emission with --tailcalls+"
                ],

                "Compiler Optimization - Modern F# Features": [
                    "Replace async { } with task { } for better performance",
                    "Use ValueTask for high-frequency async operations",
                    "Leverage IAsyncEnumerable for streaming data",
                    "Apply [<InlineIfLambda>] for higher-order functions"
                ],

                "Code-Level - Loop Optimization": [
                    "Unroll small, fixed-size loops",
                    "Hoist invariant calculations outside loops",
                    "Use for loops instead of Seq.iter for arrays",
                    "Minimize allocations inside loops"
                ],

                "Code-Level - String Handling": [
                    "Replace string concatenation with StringBuilder",
                    "Use String.Create for known-size strings",
                    "Cache frequently-used string values",
                    "Consider interning for repeated string literals"
                ],

                "Code-Level - Numeric Optimization": [
                    "Use primitive types directly (avoid generic math where possible)",
                    "Replace pown with specific power operations",
                    "Leverage SIMD operations via System.Numerics.Vectors",
                    "Use bit manipulation for power-of-2 operations"
                ],

                "Architecture - Caching Strategies": [
                    "Implement memoization for pure functions",
                    "Use ConcurrentDictionary for thread-safe caching",
                    "Apply time-based or size-based cache eviction",
                    "Consider distributed caching for scale-out scenarios"
                ],

                "Architecture - Parallelization": [
                    "Identify embarrassingly parallel operations",
                    "Use Array.Parallel for data parallelism",
                    "Implement Async.Parallel for I/O parallelism",
                    "Consider TPL Dataflow for pipeline parallelism"
                ],

                "Architecture - I/O Optimization": [
                    "Use async/await for all I/O operations",
                    "Implement buffering for file/network operations",
                    "Batch database queries",
                    "Use streaming for large data processing"
                ]
            }

    current_fastest = float('inf')
    best_project = fsharp_project_path
    iteration = 0
    for improvement in improvement_checklist:
        print(f"Improvement: {improvement}")
        for item in improvement_checklist[improvement]:
            iteration = iteration + 1
            print(f"  - {item}")
            new_project = fsharp_project_path + f"_{iteration}"
            create_project(best_project, new_project)
            code = read_file(new_project, fsharp_file)
            if iteration == 1:
                new_code = code
            else:
                new_code = coding_agent(
                    f"Analyze the following F# code, apply the improvement and only return the code: {item}\n\n{code}"
                )
                new_code = new_code.__str__().split("```fsharp")[1].split("```")[0].strip()
            write_file(new_project, fsharp_file, new_code)
            print(f"new source:{new_project}")
            fsharp_benchmark = FSharpBenchmark()
            try:
                evaluation = fsharp_benchmark.run(new_project, ["tests.txt", "expected.txt"])
                if "FAILED" in evaluation:
                    # TODO: Improvement here try to fix the error with a model
                    continue
            except:
                print(f"Error running benchmark on {new_project}. Skipping.")
                continue
            benchmark = fsharp_benchmark.run(new_project, ["--benchmark"])
            if "success" in benchmark and benchmark["success"]:
                print("Benchmarking successful")
                print(benchmark["insights"])
                fastest = benchmark["insights"][0]["mean"]
                print(f"Fastest time: {fastest}ms")
                if fastest < current_fastest:
                    current_fastest = fastest
                    print(f"New fastest time: {current_fastest}ms")
                    best_project = new_project


