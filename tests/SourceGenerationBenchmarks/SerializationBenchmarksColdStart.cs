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
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace SourceGenerationBenchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 15,
    warmupCount: 0, iterationCount: 1)] //This is used to simulate the cold start
public class SerializationBenchmarksColdStart : BaseSerializationBenchmarks
{
    private bool _setup;

    [GlobalSetup]
    public void GlobalSetup()
    {
        GenerateData();
    }

    [Benchmark]
    [BenchmarkCategory("String", "Serialize", "Base")]
    public List<string> Serialize_String_Base() => _docs1.Select(d => d.ToJson()).ToList();

    [Benchmark]
    [BenchmarkCategory("String", "Deserialize", "Base")]
    public List<TestDocument1> Deserialize_String_Base() => _jsons.Select(j => BsonSerializer.Deserialize<TestDocument1>(j)).ToList();

    [Benchmark]
    [BenchmarkCategory("String", "Serialize", "Generated")]
    public List<string> Serialize_String_Generated()
    {
        RegisterGeneratedSerializerIfNecessary();
        return _docs2.Select(d => d.ToJson()).ToList();
    }

    [Benchmark]
    [BenchmarkCategory("String", "Deserialize", "Generated")]
    public List<TestDocument2> Deserialize_String_Generated()
    {
        RegisterGeneratedSerializerIfNecessary();
        return _jsons.Select(j => BsonSerializer.Deserialize<TestDocument2>(j)).ToList();
    }

    [Benchmark]
    [BenchmarkCategory("Binary", "Serialize", "Base")]
    public List<byte[]> Serialize_Binary_Base() => _docs1.Select(d => d.ToBson()).ToList();

    [Benchmark]
    [BenchmarkCategory("Binary", "Deserialize", "Base")]
    public List<TestDocument1> Deserialize_Binary_Base() => _bsons.Select(j => BsonSerializer.Deserialize<TestDocument1>(j)).ToList();

    [Benchmark]
    [BenchmarkCategory("Binary", "Serialize", "Generated")]
    public List<byte[]> Serialize_Binary_Generated()
    {
        RegisterGeneratedSerializerIfNecessary();
        return _docs2.Select(d => d.ToBson()).ToList();
    }

    [Benchmark]
    [BenchmarkCategory("Binary", "Deserialize", "Generated")]
    public List<TestDocument2> Deserialize_Binary_Generated()
    {
        RegisterGeneratedSerializerIfNecessary();
        return _bsons.Select(j => BsonSerializer.Deserialize<TestDocument2>(j)).ToList();
    }

    //Even if we have iterationCount 1, BenchmarkDotNet will call the method twice, the first is in order to warm up the JIT
    private void RegisterGeneratedSerializerIfNecessary()
    {
        if (_setup)
        {
            BsonSerializer.RegisterSerializer(new TestDocument2Serializer());
            _setup = true;
        }
    }
}