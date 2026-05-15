# MongoDB.Bson.SourceGeneration.Benchmarks

BenchmarkDotNet comparison of the reflection-based BSON path against the source-generated path
for representative POCO shapes. Reports throughput and allocations per (de)serialize call.

## What's measured

Steady-state per-op cost. Both serializers are constructed and warmed in `GlobalSetup`; the
benchmark bodies only call `Serialize` / `Deserialize`. Streams + readers/writers are reused
across iterations so the only allocations BenchmarkDotNet attributes to the op are the ones
the serializer itself produces.

Three POCO shapes:

| Class | Path exercised |
| --- | --- |
| `SimplePocoBenchmark` | Primitives only — best case for both paths. Pure dispatch cost. |
| `NestedPocoBenchmark` | In-context nested POCO. Source-gen emits direct `s_addressSerializer.Deserialize(...)`; reflection looks up `Address` through the registry. |
| `AttributedPocoBenchmark` | `[BsonId]`, `[BsonElement]`, `[BsonRepresentation]` on `int` and an enum, `[BsonIgnoreIfNull]`, `[BsonIgnore]` — exercises the per-member cached-serializer fields. |

Reflection is the baseline (`[Benchmark(Baseline = true)]`); the source-gen row reports the
ratio in the BDN output. `[MemoryDiagnoser]` is enabled on every class for allocations.

## Running

```sh
# All benchmarks
dotnet run -c Release --project benchmarks/MongoDB.Bson.SourceGeneration.Benchmarks -- --filter '*'

# Just one class
dotnet run -c Release --project benchmarks/MongoDB.Bson.SourceGeneration.Benchmarks -- --filter '*SimplePoco*'

# Just one method
dotnet run -c Release --project benchmarks/MongoDB.Bson.SourceGeneration.Benchmarks -- --filter '*Serialize_SourceGen*'
```

Default BDN runs spin up a separate process per benchmark and take a few minutes. For quick
sanity checks add `--job short` (`[ShortRunJob]` semantics) — measurements are noisier but
results land in under a minute.

## Cold-start harness

BenchmarkDotNet can't measure cold start fairly for source-gen — the generator's
`GeneratedProvider` cached fields are `private static readonly` and fire exactly once per
AppDomain, so every BDN iteration after the first reuses them. Instead, `ColdStartHarness`
runs in a fresh process per measurement (one Stopwatch'd run, exit, repeat via shell loop).

```sh
# Build once
dotnet build -c Release benchmarks/MongoDB.Bson.SourceGeneration.Benchmarks

# Single shot per mode
DLL=benchmarks/MongoDB.Bson.SourceGeneration.Benchmarks/bin/Release/net8.0/MongoDB.Bson.SourceGeneration.Benchmarks.dll
dotnet "$DLL" --cold-start reflection
dotnet "$DLL" --cold-start sourcegen

# 20-run loop with aggregation lives in `scripts/cold-start.sh` (or run inline; see the
# repo's commit that introduced this file for the exact awk aggregation).
```

The harness times three phases separately so you can see where each path spends its time:

- `register_ns` — source-gen only: `BenchmarkContext.Default.Register()` materialises the
  provider (fires every `XSerializer`'s static field initializer) and pushes it onto the
  `BsonSerializer` provider stack. Zero on the reflection path (the registry is lazy).
- `lookup_ns` — first `LookupSerializer<T>()`-equivalent. Reflection pays `BsonClassMap`
  construction + the convention chain + `Expression.Compile` for member accessors here.
  Source-gen pays a single cached-field load.
- `serialize_ns` — first `Serialize(...)` call (includes JIT for the serialize method itself).

## Sample results

Steady-state (BDN `--job short`, Apple M1 Max, .NET 8.0.5, ARM64 RyuJIT, 2026-05-15):

**SimplePoco** (5 primitive members):

| Method                 |     Mean | Ratio |    Allocated | Alloc Ratio |
|------------------------|---------:|------:|-------------:|------------:|
| Serialize_Reflection   | 303.6 ns |  1.00 |        104 B |        1.00 |
| Serialize_SourceGen    | 225.3 ns |  0.74 |          0 B |        0.00 |
| Deserialize_Reflection | 509.0 ns |  1.68 |        840 B |        8.08 |
| Deserialize_SourceGen  | 365.8 ns |  1.20 |        872 B |        8.38 |

**NestedPoco** (two in-context `Address` members):

| Method                 |       Mean | Ratio |    Allocated | Alloc Ratio |
|------------------------|-----------:|------:|-------------:|------------:|
| Serialize_Reflection   |   750.4 ns |  1.00 |         32 B |        1.00 |
| Serialize_SourceGen    |   604.4 ns |  0.81 |          0 B |        0.00 |
| Deserialize_Reflection | 1,307.4 ns |  1.74 |       1776 B |       55.50 |
| Deserialize_SourceGen  |   954.9 ns |  1.27 |       2080 B |       65.00 |

**AttributedPoco** (`[BsonRepresentation]` on int + enum + ignore/element/required):

| Method                 |     Mean | Ratio |    Allocated | Alloc Ratio |
|------------------------|---------:|------:|-------------:|------------:|
| Serialize_Reflection   | 374.5 ns |  1.00 |        104 B |        1.00 |
| Serialize_SourceGen    | 295.4 ns |  0.79 |         24 B |        0.23 |
| Deserialize_Reflection | 611.0 ns |  1.63 |        944 B |        9.08 |
| Deserialize_SourceGen  | 473.7 ns |  1.26 |       1008 B |        9.69 |

Headlines:

- **Serialize is ~20–26% faster** under source-gen across all three shapes, and **drops allocations from ~100 B/op to 0 B/op for the simple shape** (and 24 B/op for the attributed one). The reflection path's per-op allocations come from boxing during accessor delegate invocation; source-gen calls the property setter/getter directly.
- **Deserialize is ~22–28% faster** under source-gen. Allocations are similar between paths (both pay for the resulting POCO instance + string members); this is dominated by user-data size rather than dispatch overhead.

Cold-start (one-shot per process, 20-run average, same hardware):

| Phase                  | Reflection | Source-gen |       Delta |
|------------------------|-----------:|-----------:|------------:|
| Register               |       0 ms |   13.63 ms |     +13.63  |
| First lookup           |   27.68 ms |    0.19 ms |    **−27.49** |
| First serialize        |    9.95 ms |    5.47 ms |     **−4.48** |
| **Total**              | **37.64 ms** | **19.29 ms** | **≈2× faster** |

The `Register` cost on the source-gen side amortises across every type in the context — for a context with 100 listed types, the per-type register cost would still be in the same ballpark, but reflection's ~28 ms first-lookup cost would compound linearly per cold type, so the source-gen win grows with the type count.

## What's not measured (yet)

- **AOT publish.** Currently blocked by the upstream `MongoDB.Bson` TFM list (`NETSDK1207`).
  See `claude_notes/skunk112/PLAN.md` ticket #7.
- **End-to-end driver call** (Insert / Find through `MongoClient`). The serialization layer is
  one step of many; for full driver throughput numbers see `benchmarks/MongoDB.Driver.Benchmarks`.
