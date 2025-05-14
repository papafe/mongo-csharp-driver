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
    [Params(1000)]
    public int CountDocuments;

    private List<ComplexTestDocument> _docs;
    protected List<ComplexTestDocument1> _docs1;
    protected List<ComplexTestDocument2> _docs2;
    protected List<string> _jsons;
    protected List<byte[]> _bsons;

    protected void GenerateDocuments()
    {
        _docs = Enumerable.Range(0, CountDocuments).Select(i => new ComplexTestDocument
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

        _docs1 = _docs.Select(d => new ComplexTestDocument1
        {
            Id = d.Id,
            Name = d.Name,
            Metadata = new Metadata1 { Category = d.Metadata.Category, Timestamp = d.Metadata.Timestamp },
            Items = d.Items.Select(i => new Item1 { Label = i.Label, Value = i.Value }).ToList()
        }).ToList();

        _docs2 = _docs.Select(d => new ComplexTestDocument2
        {
            Id = d.Id,
            Name = d.Name,
            Metadata = new Metadata2 { Category = d.Metadata.Category, Timestamp = d.Metadata.Timestamp },
            Items = d.Items.Select(i => new Item2 { Label = i.Label, Value = i.Value }).ToList()
        }).ToList();

        _jsons = _docs.Select(d => d.ToJson()).ToList();
        _bsons = _docs.Select(d => d.ToBson()).ToList();
    }

    public class ComplexTestDocument
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

    public class ComplexTestDocument1
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

    public class ComplexTestDocument2
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

    protected class ComplexTestDocument2Serializer : ClassSerializerBase<ComplexTestDocument2>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, ComplexTestDocument2 value)
        {
            context.Writer.WriteStartDocument();
            context.Writer.WriteInt32(nameof(ComplexTestDocument2.Id), value.Id);
            context.Writer.WriteString(nameof(ComplexTestDocument2.Name), value.Name);

            context.Writer.WriteName(nameof(ComplexTestDocument2.Metadata));
            context.Writer.WriteStartDocument();
            context.Writer.WriteString(nameof(Metadata1.Category), value.Metadata.Category);
            context.Writer.WriteDateTime(nameof(Metadata1.Timestamp), BsonUtils.ToMillisecondsSinceEpoch(value.Metadata.Timestamp.ToUniversalTime()));
            context.Writer.WriteEndDocument();

            context.Writer.WriteName(nameof(ComplexTestDocument2.Items));
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

        public override ComplexTestDocument2 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
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
                    case nameof(ComplexTestDocument2.Id):
                        id = context.Reader.ReadInt32();
                        break;
                    case nameof(ComplexTestDocument2.Name):
                        name = context.Reader.ReadString();
                        break;
                    case nameof(ComplexTestDocument2.Metadata):
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
                    case nameof(ComplexTestDocument2.Items):
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

            return new ComplexTestDocument2
            {
                Id = id,
                Name = name,
                Metadata = metadata,
                Items = items
            };
        }
    }
}