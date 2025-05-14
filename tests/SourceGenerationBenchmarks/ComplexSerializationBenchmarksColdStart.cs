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
[SimpleJob(RunStrategy.Monitoring, launchCount: 10,
    warmupCount: 0, iterationCount: 1)]
public class ComplexSerializationBenchmarksColdStart : BaseSerializationBenchmarks
{
    private bool _setup;

    [GlobalSetup(Targets = [nameof(Serialize_Base), nameof(Deserialize_Base)])]
    public void GlobalSetupBase()
    {
        GenerateDocuments();
        _jsons = _docs2.Select(d => d.ToJson()).ToList();
        //TODO Don't like it this, way
        //It's done like this so we don't already create the serializer for the type
    }

    [GlobalSetup(Targets = [nameof(Serialize_Generated), nameof(Deserialize_Generated)])]
    public void GlobalSetupGenerated()
    {
        GenerateDocuments();
        _jsons = _docs1.Select(d => d.ToJson()).ToList();
    }

    [Benchmark]
    public List<string> Serialize_Base()
    {
        return _docs1.Select(d => d.ToJson()).ToList();
    }

    [Benchmark]
    public List<ComplexTestDocument1> Deserialize_Base()
    {
        return _jsons.Select(j => BsonSerializer.Deserialize<ComplexTestDocument1>(j)).ToList();
    }

    [Benchmark]
    public List<string> Serialize_Generated()
    {
        RegisterGeneratedSerializerIfNecessary();
        return _docs2.Select(d => d.ToJson()).ToList();
    }

    [Benchmark]
    public List<ComplexTestDocument2> Deserialize_Generated()
    {
        RegisterGeneratedSerializerIfNecessary();
        return _jsons.Select(j => BsonSerializer.Deserialize<ComplexTestDocument2>(j)).ToList();
    }

    private void RegisterGeneratedSerializerIfNecessary()
    {
        if (_setup)
        {
            BsonSerializer.RegisterSerializer(new ComplexTestDocument2Serializer());
            _setup = true;
        }
    }
}