# Source-Generated Serialization Performance Analysis

## Introduction

This report summarizes an investigation into the performance of source-generated serializers compared to baseline runtime serializers in the MongoDB C# driver. The goal was to assess whether source generation offers meaningful advantages across different workloads and runtimes, particularly in scenarios sensitive to cold starts (like serverless apps).

## Test Matrix

We tested combinations of:

* **Workloads**:

  * **Classic**: Serialization/deserialization in a long-running app.
  * **Cold Start**: Short-lived app simulating lambda/serverless scenarios.

* **Runtimes**:

  * **Classic (JIT)**: Standard .NET runtime.
  * **AOT**: NativeAOT published runtime.

Each test was executed for three different input sizes: 100, 1,000, and 10,000 documents. This helped evaluate performance under various loads. Each test was run for both baseline (reflection-based) and source-generated serializers, except in NativeAOT where baseline could not be used.

## Results Summary

### 🔁 Source-Generated vs Baseline

* **Deserialization** showed the largest gains: source-generated was \~40–50% faster and used \~20% less memory allocation across all input sizes.
* **Serialization** gains were more modest but consistent: source-generated was \~15–30% faster and slightly leaner in memory use.
* These improvements were more noticeable at lower document counts, where runtime overhead like reflection, expression compilation, and delegate caching has a proportionally larger impact.

### ❄️ Cold Start vs Classic Workload

* Cold start workloads had a significant penalty in the baseline (JIT) version, with execution time increasing by 5–7× compared to the classic workload.
* The source-generated version was much less affected by cold start conditions, with performance remaining close to the classic workload.

### ⚙️ AOT Observations

* **Source-generated code on AOT** performed comparably to JIT under the classic workload.
* **AOT cold start was very fast**, with minimal additional overhead compared to the warm runs.
* ⚠️ The baseline implementation could not run on AOT due to reliance on unsupported features like expression trees and runtime code generation.
* ✅ A minor adjustment was made to the source-generated path to run under AOT: a dynamic serializer registration using reflection was being triggered unnecessarily and was commented out.

## Notes on Serializer Implementation

* The **source-generated serializer** used in these tests was **manually written**.
* It may lack some of the robustness and edge-case handling found in the official baseline serializer. For example, it does not support extra properties.
* It does not delegate serialization of nested classes to other serializers—it handles all properties inline.

This simpler structure helps reduce overhead, but also means it doesn't have feature parity with the production serializer.

## Profiling

We performed a basic profiling pass to understand where time was spent in the different serializers. This was done using `dotnet-trace` and [Speedscope](https://www.speedscope.app). While the analysis was not exhaustive, it gave useful insights into the underlying performance differences.

### Deserialization

* **Baseline** showed time spent in `Expression.Compile`, `ConcurrentDictionary.GetOrAdd`, and other reflection-heavy code paths.
* **Generated** had a shallow call stack, with most time spent directly writing into the BSON writer. No dynamic code generation or caching layers were observed.

### Serialization

* **Baseline** involved `GetGetter()`, lambda compilation, and dynamic dispatch via `BsonClassMapSerializer`.
* **Generated** executed tightly inlined loops with no runtime indirection. Calls to `IBsonWriterExtensions.WriteX` were clearly visible and dominated the hot path.

The profiled code also included the creation and registration of serializers. However, since the serialization and deserialization code was executed many times, we expect the registration overhead to have minimal impact on the overall trace results.

## Where to Find Results

* **BenchmarkDotNet results** are stored in the `Benchmarks` folder:

  * `Classic/` for classic JIT runtime results.
  * `AOT/` for NativeAOT results (only source-generated).

* **Tracing output** is in the `Traces` folder:

  * Each `.nettrace` file represents a specific method (e.g., `serialize_generated`, `deserialize_base`, etc.).
  * These were directly generated in [Speedscope JSON format](https://www.speedscope.app) using the `--format speedscope` flag and can be loaded as-is.

## How to Reproduce

### Benchmark Execution (BenchmarkDotNet)

* Benchmarks were implemented using BenchmarkDotNet.
* `[MemoryDiagnoser]` was used to capture memory allocations.
* Tests were run for 100, 1,000, and 10,000 documents for each scenario.

### Performance Tracing (dotnet-trace + speedscope)

* A helper script, `performance.sh`, is available to automate performance tracing.
* It runs the four key test modes using `dotnet-trace` and stores the resulting `.json` files in the `Traces/` folder.
* These files are fully compatible with [https://www.speedscope.app](https://www.speedscope.app) and can be visualized directly for flamegraph analysis.

## Conclusions

This investigation confirms that source-generated serializers can offer meaningful performance advantages over baseline serializers, particularly for deserialization-heavy workloads and cold start scenarios. These improvements are even more relevant in AOT environments, where dynamic features required by the baseline implementation are unsupported. While the source-generated serializer used here is simplified and lacks full feature parity, it still demonstrates clear performance benefits and provides a strong basis for future work.
