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

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace SourceGenerationBenchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("AOT")]
[SimpleJob(RunStrategy.Throughput, RuntimeMoniker.NativeAot80)]
public class SerializationBenchmarksAOT : BaseSerializationBenchmarks
{
    [GlobalSetup]
    public void GlobalSetup()
    {
        BsonSerializer.RegisterSerializer(new TestDocument2Serializer());
        GenerateDocuments();
        _bsons = _docs2.Select(d => d.ToBson()).ToList();
    }

    [Benchmark]
    [BenchmarkCategory("Binary", "Serialize", "Generated")]
    public List<byte[]> Serialize_Binary_Generated()
    {
        return _docs2.Select(d => d.ToBson()).ToList();
    }

    [Benchmark]
    [BenchmarkCategory("Binary", "Deserialize", "Generated")]
    public List<TestDocument2> Deserialize_Binary_Generated()
    {
        return _bsons.Select(j => BsonSerializer.Deserialize<TestDocument2>(j)).ToList();
    }
}