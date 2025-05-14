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
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace SourceGenerationBenchmarks;

[MemoryDiagnoser]
public class ComplexSerializationBenchmarks : BaseSerializationBenchmarks
{
    [GlobalSetup]
    public void GlobalSetup()
    {
        var classMap = new BsonClassMap<ComplexTestDocument1>(cm =>
        {
            cm.AutoMap();
        });
        classMap.Freeze();
        BsonSerializer.RegisterSerializer(typeof(ComplexTestDocument1), new BsonClassMapSerializer<ComplexTestDocument1>(classMap));

        BsonSerializer.RegisterSerializer(new ComplexTestDocument2Serializer());

        GenerateDocuments();
    }

    [Benchmark]
    public List<string> Serialize_Base() => _docs1.Select(d => d.ToJson()).ToList();

    [Benchmark]
    public List<ComplexTestDocument1> Deserialize_Base() => _jsons.Select(j => BsonSerializer.Deserialize<ComplexTestDocument1>(j)).ToList();

    [Benchmark]
    public List<string> Serialize_Generated() => _ = _docs2.Select(d => d.ToJson()).ToList();

    [Benchmark]
    public List<ComplexTestDocument2> Deserialize_Generated() => _jsons.Select(j => BsonSerializer.Deserialize<ComplexTestDocument2>(j)).ToList();
}