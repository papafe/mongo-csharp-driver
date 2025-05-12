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
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace SourceGenerationBenchmarks;

[MemoryDiagnoser]
public class SimpleSerializationBenchmarks
{
    private const int countDocument = 500;

    private List<TestDocument> _testDocuments;
    private List<string> _testJsonStrings;

    private List<TestDocument2> _testDocuments2;
    private List<string> _testJsonStrings2;

    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine("haha");
        var classMap = new BsonClassMap<TestDocument>(cm =>
        {
            cm.AutoMap();
        });
        classMap.Freeze();
        BsonSerializer.RegisterSerializer(typeof(TestDocument), new BsonClassMapSerializer<TestDocument>(classMap));

        BsonSerializer.RegisterSerializer(new TestDocument2Serializer());

        _testDocuments = Enumerable.Range(0, countDocument).Select(i => new TestDocument
        {
            Id = i,
            Name = $"Item {i}",
        }).ToList();

        _testJsonStrings = _testDocuments.Select(d => d.ToJson()).ToList();

        _testDocuments2 = Enumerable.Range(0, countDocument).Select(i => new TestDocument2
        {
            Id = i,
            Name = $"Item {i}",
        }).ToList();

        _testJsonStrings2 = _testDocuments2.Select(d => d.ToJson()).ToList();
    }

    [Benchmark]
    public void Serialize_Base()
    {
        foreach (var doc in _testDocuments)
        {
            _ = doc.ToJson();
        }
    }

    [Benchmark]
    public void Deserialize_Base()
    {
        foreach (var json in _testJsonStrings)
        {
            _ = BsonSerializer.Deserialize<TestDocument>(json);
        }
    }

    [Benchmark]
    public void Serialize_Generated()
    {
        foreach (var doc in _testDocuments2)
        {
            _ = doc.ToJson();
        }
    }

    [Benchmark]
    public void Deserialize_Generated()
    {
        foreach (var json in _testJsonStrings2)
        {
            _ = BsonSerializer.Deserialize<TestDocument2>(json);
        }
    }

    public class TestDocument
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TestDocument2
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TestDocument2Serializer : ClassSerializerBase<TestDocument2>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TestDocument2 value)
        {
            context.Writer.WriteStartDocument();
            context.Writer.WriteInt32(nameof(TestDocument2.Id), value.Id);
            context.Writer.WriteString(nameof(TestDocument2.Name), value.Name);
            context.Writer.WriteEndDocument();
        }

        public override TestDocument2 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartDocument();
            var id = context.Reader.ReadInt32(nameof(TestDocument2.Id));
            var name = context.Reader.ReadString(nameof(TestDocument2.Name));
            context.Reader.ReadEndDocument();
            return new TestDocument2 { Id = id, Name = name };
        }
    }

    /* Possible improvements:
     * - Try to spell out WriteInt32 instead of using default implementation, so that's faster, maybe
     * - Use a complex test document with more properties
     * - Use a complex test document with nested properties
     * -
     *
     */
}