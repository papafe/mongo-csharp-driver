/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Diagnostics;
using System.IO;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.SourceGeneration.Benchmarks.Models;

namespace MongoDB.Bson.SourceGeneration.Benchmarks
{
    // One-shot cold-start harness. BenchmarkDotNet can't measure cold start fairly for source-gen
    // because the generator's `GeneratedProvider` cached fields are `private static readonly` and
    // fire exactly once per AppDomain — every BDN iteration after the first reuses them. So we
    // approximate "AOT user experience" by running the harness once per process and letting a
    // shell loop drive repetitions, the same way a fresh request would.
    //
    // Phases timed separately so the aggregator can break the cost down:
    //   - Register: source-gen's `BenchmarkContext.Default.Register()` (materialises the provider
    //     and pushes it onto the BsonSerializer stack). Free for reflection.
    //   - Lookup: first `LookupSerializer<T>()`-equivalent call. Reflection pays BsonClassMap
    //     construction + Expression.Compile here; source-gen pays one cached-field load.
    //   - Serialize: first Serialize call. Same shape for both.
    //
    // We bypass the BsonSerializer registry on the reflection side (build a serializer directly
    // from a freshly-AutoMapped class map) so the comparison is "what does the reflection pipeline
    // cost in isolation" — equivalent to what users hit on first use through the registry.
    internal static class ColdStartHarness
    {
        public static int Run(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: --cold-start <reflection|sourcegen>");
                return 1;
            }

            // Sample value is shared across both modes so output bytes are identical.
            var sample = new SimplePoco
            {
                Id = ObjectId.GenerateNewId(),
                Name = "Ada Lovelace",
                Age = 35,
                IsActive = true,
                Score = 42.5
            };

            using var stream = new MemoryStream(capacity: 256);
            using var writer = new BsonBinaryWriter(stream);
            var ctx = BsonSerializationContext.CreateRoot(writer);

            return args[0] switch
            {
                "reflection" => RunReflection(sample, stream, ctx),
                "sourcegen" => RunSourceGen(sample, stream, ctx),
                _ => Usage()
            };
        }

        private static int Usage()
        {
            Console.Error.WriteLine("usage: --cold-start <reflection|sourcegen>");
            return 1;
        }

        private static int RunReflection(SimplePoco sample, MemoryStream stream, BsonSerializationContext ctx)
        {
            // No "register" step on the reflection path — the registry serves the type on demand.
            var registerNs = 0L;

            var sw = Stopwatch.StartNew();
            // BsonClassMap.AutoMap walks the type via reflection and applies the convention chain.
            // BsonMemberMap accessor compilation happens inside Freeze().
            var classMap = new BsonClassMap<SimplePoco>(cm => cm.AutoMap());
            classMap.Freeze();
            var serializer = new BsonClassMapSerializer<SimplePoco>(classMap);
            sw.Stop();
            var lookupNs = sw.ElapsedTicks * (1_000_000_000L / Stopwatch.Frequency);

            sw.Restart();
            serializer.Serialize(ctx, sample);
            sw.Stop();
            var serializeNs = sw.ElapsedTicks * (1_000_000_000L / Stopwatch.Frequency);

            Console.WriteLine($"mode=reflection register_ns={registerNs} lookup_ns={lookupNs} serialize_ns={serializeNs} bytes={stream.Position}");
            return 0;
        }

        private static int RunSourceGen(SimplePoco sample, MemoryStream stream, BsonSerializationContext ctx)
        {
            var sw = Stopwatch.StartNew();
            // Register() materialises the provider lazily via `BenchmarkContext.Default.Provider`,
            // which forces the static-field initializers of `GeneratedProvider` to run — that's
            // where every `new XSerializer()` for every listed type happens. Source-gen's
            // "first-use cost" lives entirely here.
            BenchmarkContext.Default.Register();
            sw.Stop();
            var registerNs = sw.ElapsedTicks * (1_000_000_000L / Stopwatch.Frequency);

            sw.Restart();
            var serializer = (IBsonSerializer<SimplePoco>)BenchmarkContext.Default.Provider.GetSerializer(typeof(SimplePoco))!;
            sw.Stop();
            var lookupNs = sw.ElapsedTicks * (1_000_000_000L / Stopwatch.Frequency);

            sw.Restart();
            serializer.Serialize(ctx, sample);
            sw.Stop();
            var serializeNs = sw.ElapsedTicks * (1_000_000_000L / Stopwatch.Frequency);

            Console.WriteLine($"mode=sourcegen register_ns={registerNs} lookup_ns={lookupNs} serialize_ns={serializeNs} bytes={stream.Position}");
            return 0;
        }
    }
}
