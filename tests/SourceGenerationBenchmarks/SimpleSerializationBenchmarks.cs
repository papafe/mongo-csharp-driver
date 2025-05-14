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
using BenchmarkDotNet.Order;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace SourceGenerationBenchmarks;

//[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.Method)]
public class SimpleSerializationBenchmarks
{
    [Params(5000)]
    public int CountDocuments;

    private List<TestDocument> _testDocuments;
    private List<TestDocument2> _testDocuments2;
    private List<TestDocument3> _testDocuments3;

    private List<string> _testJsonStrings;

    [GlobalSetup]
    public void Setup()
    {
        Console.WriteLine($"Setting up SimpleSerializationBenchmarks... for {CountDocuments}");

        var classMap = new BsonClassMap<TestDocument>(cm =>
        {
            cm.AutoMap();
        });
        classMap.Freeze();
        BsonSerializer.RegisterSerializer(typeof(TestDocument), new BsonClassMapSerializer<TestDocument>(classMap));

        BsonSerializer.RegisterSerializer(new TestDocument2Serializer());

        _testDocuments = Enumerable.Range(0, CountDocuments).Select(i => new TestDocument
        {
            Id = i,
            Name = $"Item {i}",
        }).ToList();

        _testDocuments2 = Enumerable.Range(0, CountDocuments).Select(i => new TestDocument2
        {
            Id = i,
            Name = $"Item {i}",
        }).ToList();

        _testDocuments3 = Enumerable.Range(0, CountDocuments).Select(i => new TestDocument3
        {
            Id = i,
            Name = $"Item {i}",
        }).ToList();

        _testJsonStrings = _testDocuments.Select(d => d.ToJson()).ToList();
    }

    [Benchmark]
    public void Serialize_Base() => _ = _testDocuments.Select(d => d.ToJson()).ToList();

    [Benchmark]
    public void Deserialize_Base() => _ = _testJsonStrings.Select(j => BsonSerializer.Deserialize<TestDocument>(j)).ToList();

    [Benchmark]
    public void Serialize_Generated() => _ = _testDocuments2.Select(d => d.ToJson()).ToList();

    [Benchmark]
    public void Deserialize_Generated() => _ = _testJsonStrings.Select(j => BsonSerializer.Deserialize<TestDocument2>(j)).ToList();

    [Benchmark]
    public void Serialize_Generated_Interface() => _ = _testDocuments3.Select(d => d.ToJson()).ToList();

    [Benchmark]
    public void Deserialize_Generated_Interface() => _ = _testJsonStrings.Select(j => BsonSerializer.Deserialize<TestDocument3>(j)).ToList();

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

    public class TestDocument3
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
            int id = 0;
            string name = null;

            while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var propertyName = context.Reader.ReadName();
                switch (propertyName)
                {
                    case nameof(TestDocument2.Id):
                        id = context.Reader.ReadInt32();
                        break;
                    case nameof(TestDocument2.Name):
                        name = context.Reader.ReadString();
                        break;
                    default:
                        context.Reader.SkipValue();
                        break;
                }
            }

            context.Reader.ReadEndDocument();
            return new TestDocument2 { Id = id, Name = name };
        }
    }

    public class TestDocument3Serializer : IBsonSerializer<TestDocument3>
    {
        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TestDocument3 value)
        {
            context.Writer.WriteStartDocument();
            context.Writer.WriteInt32(nameof(TestDocument2.Id), value.Id);
            context.Writer.WriteString(nameof(TestDocument2.Name), value.Name);
            context.Writer.WriteEndDocument();
        }

        public TestDocument3 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartDocument();
            int id = 0;
            string name = null;

            while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var propertyName = context.Reader.ReadName();
                switch (propertyName)
                {
                    case nameof(TestDocument2.Id):
                        id = context.Reader.ReadInt32();
                        break;
                    case nameof(TestDocument2.Name):
                        name = context.Reader.ReadString();
                        break;
                    default:
                        context.Reader.SkipValue();
                        break;
                }
            }

            context.Reader.ReadEndDocument();
            return new TestDocument3 { Id = id, Name = name };
        }

        //
        public Type ValueType => typeof(TestDocument3);

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return Deserialize(context, args);
        }

        void IBsonSerializer.Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            Serialize(context, args, (TestDocument3)value);
        }
    }
}