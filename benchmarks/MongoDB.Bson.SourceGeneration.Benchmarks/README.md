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

## What's not measured (yet)

- **First-use / cold-start cost.** Reflection builds `BsonClassMap` + `Expression.Compile` on
  the first lookup; source-gen pays only the cached-field instantiation when the provider is
  materialised. Comparing those is a separate benchmark — the global static state in
  `BsonSerializer` makes it hard to reset between BDN iterations cleanly, so this would need
  a single-shot harness rather than a BDN class. Add when there's a real-user question to
  answer.
- **AOT publish.** Currently blocked by the upstream `MongoDB.Bson` TFM list (`NETSDK1207`).
  See `claude_notes/skunk112/PLAN.md` ticket #7.
- **End-to-end driver call** (Insert / Find through `MongoClient`). The serialization layer is
  one step of many; for full driver throughput numbers see `benchmarks/MongoDB.Driver.Benchmarks`.
