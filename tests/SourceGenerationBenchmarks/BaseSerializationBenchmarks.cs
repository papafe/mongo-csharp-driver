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

public class BaseSerializationBenchmarks
{
    [Params(100, 1000, 10000)]
    public int CountDocuments;

    protected List<TestDocument> _docs;
    protected List<TestDocument1> _docs1;
    protected List<TestDocument2> _docs2;
    protected List<TestDocument3> _docs3;
    protected List<string> _jsons;
    protected List<byte[]> _bsons;

    protected void GenerateData()
    {
        GenerateDocuments();

        _jsons = _docs.Select(d => d.ToJson()).ToList();
        _bsons = _docs.Select(d => d.ToBson()).ToList();
    }

    protected void GenerateDocuments()
    {
        _docs = Enumerable.Range(0, CountDocuments).Select(i => new TestDocument
        {
            Id = i,
            Name = $"Doc {i}",
            Metadata = new Metadata { Category = "alpha", Timestamp = DateTime.UtcNow },
            Items =
            [
                new() { Label = "a", Value = i },
                new() { Label = "b", Value = i }
            ]
        }).ToList();

        _docs1 = _docs.Select(d => new TestDocument1
        {
            Id = d.Id,
            Name = d.Name,
            Metadata = new Metadata1 { Category = d.Metadata.Category, Timestamp = d.Metadata.Timestamp },
            Items = d.Items.Select(i => new Item1 { Label = i.Label, Value = i.Value }).ToList()
        }).ToList();

        _docs2 = _docs.Select(d => new TestDocument2
        {
            Id = d.Id,
            Name = d.Name,
            Metadata = new Metadata2 { Category = d.Metadata.Category, Timestamp = d.Metadata.Timestamp },
            Items = d.Items.Select(i => new Item2 { Label = i.Label, Value = i.Value }).ToList()
        }).ToList();

        _docs3 = _docs.Select(d => new TestDocument3
        {
            Id = d.Id,
            Name = d.Name,
            Metadata = new Metadata3 { Category = d.Metadata.Category, Timestamp = d.Metadata.Timestamp },
            Items = d.Items.Select(i => new Item3 { Label = i.Label, Value = i.Value }).ToList()
        }).ToList();
    }

    public class TestDocument
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Metadata Metadata { get; set; }
        public List<Item> Items { get; set; }
    }

    public class Metadata
    {
        public string Category { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Item
    {
        public string Label { get; set; }
        public double Value { get; set; }
    }

    public class TestDocument1
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Metadata1 Metadata { get; set; }
        public List<Item1> Items { get; set; }
    }

    public class Metadata1
    {
        public string Category { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Item1
    {
        public string Label { get; set; }
        public double Value { get; set; }
    }

    public class TestDocument2
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Metadata2 Metadata { get; set; }
        public List<Item2> Items { get; set; }
    }

    public class Metadata2
    {
        public string Category { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Item2
    {
        public string Label { get; set; }
        public double Value { get; set; }
    }

    public class TestDocument3
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Metadata3 Metadata { get; set; }
        public List<Item3> Items { get; set; }
    }

    public class Metadata3
    {
        public string Category { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Item3
    {
        public string Label { get; set; }
        public double Value { get; set; }
    }

    protected class TestDocument2Serializer : ClassSerializerBase<TestDocument2>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TestDocument2 value)
        {
            context.Writer.WriteStartDocument();
            context.Writer.WriteInt32(nameof(TestDocument2.Id), value.Id);
            context.Writer.WriteString(nameof(TestDocument2.Name), value.Name);

            context.Writer.WriteName(nameof(TestDocument2.Metadata));
            context.Writer.WriteStartDocument();
            context.Writer.WriteString(nameof(Metadata1.Category), value.Metadata.Category);
            context.Writer.WriteDateTime(nameof(Metadata1.Timestamp), BsonUtils.ToMillisecondsSinceEpoch(value.Metadata.Timestamp.ToUniversalTime()));
            context.Writer.WriteEndDocument();

            context.Writer.WriteName(nameof(TestDocument2.Items));
            context.Writer.WriteStartArray();
            foreach (var item in value.Items)
            {
                context.Writer.WriteStartDocument();
                context.Writer.WriteString(nameof(Item1.Label), item.Label);
                context.Writer.WriteDouble(nameof(Item1.Value), item.Value);
                context.Writer.WriteEndDocument();
            }
            context.Writer.WriteEndArray();

            context.Writer.WriteEndDocument();
        }

        public override TestDocument2 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartDocument();
            var id = 0;
            string name = null;
            Metadata2 metadata = null;
            var items = new List<Item2>();

            while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
            {
                var fieldName = context.Reader.ReadName();
                switch (fieldName)
                {
                    case nameof(TestDocument2.Id):
                        id = context.Reader.ReadInt32();
                        break;
                    case nameof(TestDocument2.Name):
                        name = context.Reader.ReadString();
                        break;
                    case nameof(TestDocument2.Metadata):
                        context.Reader.ReadStartDocument();
                        var category = string.Empty;
                        DateTime timestamp = default;
                        while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
                        {
                            var metadataField = context.Reader.ReadName();
                            switch (metadataField)
                            {
                                case nameof(Metadata1.Category):
                                    category = context.Reader.ReadString();
                                    break;
                                case nameof(Metadata1.Timestamp):
                                    timestamp = new BsonDateTime(context.Reader.ReadDateTime()).ToUniversalTime();
                                    break;
                            }
                        }
                        context.Reader.ReadEndDocument();
                        metadata = new Metadata2 { Category = category, Timestamp = timestamp };
                        break;
                    case nameof(TestDocument2.Items):
                        context.Reader.ReadStartArray();
                        while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
                        {
                            context.Reader.ReadStartDocument();
                            var label = string.Empty;
                            double value = 0;
                            while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
                            {
                                var itemField = context.Reader.ReadName();
                                switch (itemField)
                                {
                                    case nameof(Item1.Label):
                                        label = context.Reader.ReadString();
                                        break;
                                    case nameof(Item1.Value):
                                        value = context.Reader.ReadDouble();
                                        break;
                                }
                            }
                            context.Reader.ReadEndDocument();
                            items.Add(new Item2 { Label = label, Value = value });
                        }
                        context.Reader.ReadEndArray();
                        break;
                    default:
                        context.Reader.SkipValue();
                        break;
                }
            }

            context.Reader.ReadEndDocument();

            return new TestDocument2
            {
                Id = id,
                Name = name,
                Metadata = metadata,
                Items = items
            };
        }
    }

    //This is used only for creating values in the setup stage, it's not used in the benchmarks.
    protected class TestDocument3Serializer : ClassSerializerBase<TestDocument3>
    {
        private readonly TestDocument2Serializer _innerSerializer = new();

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args,
            TestDocument3 value)
        {
            var testDocument2 = new TestDocument2
            {
                Id = value.Id,
                Name = value.Name,
                Metadata = new Metadata2
                {
                    Category = value.Metadata.Category,
                    Timestamp = value.Metadata.Timestamp
                },
                Items = value.Items.Select(i => new Item2 { Label = i.Label, Value = i.Value }).ToList()
            };

            _innerSerializer.Serialize(context, args, testDocument2);
        }

        public override TestDocument3 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var testDocument2 = _innerSerializer.Deserialize(context, args);

            return new TestDocument3
            {
                Id = testDocument2.Id,
                Name = testDocument2.Name,
                Metadata = new Metadata3
                {
                    Category = testDocument2.Metadata.Category,
                    Timestamp = testDocument2.Metadata.Timestamp
                },
                Items = testDocument2.Items.Select(i => new Item3 { Label = i.Label, Value = i.Value }).ToList()
            };
        }
    }
}