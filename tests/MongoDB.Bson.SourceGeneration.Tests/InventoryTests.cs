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

using FluentAssertions;
using MongoDB.Bson.Serialization;
using Xunit;
using static MongoDB.Bson.SourceGeneration.Tests.SerializationTestHelpers;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    public class InventoryTests
    {
        static InventoryTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Unknown_Elements_Stashed_Into_ExtraElements_On_Deserialize()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Count", 5 },
                { "unknownString", "hello" },
                { "unknownInt", 42 }
            }.ToBson();

            var result = BsonSerializer.Deserialize<Inventory>(wire);

            result.Count.Should().Be(5);
            result.Extras.Should().NotBeNull();
            result.Extras["unknownString"].AsString.Should().Be("hello");
            result.Extras["unknownInt"].AsInt32.Should().Be(42);
        }

        [Fact]
        public void Empty_Extras_Stays_Null_When_No_Unknown_Elements_Seen()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Count", 7 }
            }.ToBson();

            var result = BsonSerializer.Deserialize<Inventory>(wire);

            result.Count.Should().Be(7);
            result.Extras.Should().BeNull();
        }

        [Fact]
        public void ExtraElements_Round_Trip_Preserves_All_Values()
        {
            var original = NewSample();
            var bytes = original.ToBson();
            var result = BsonSerializer.Deserialize<Inventory>(bytes);

            result.Id.Should().Be(original.Id);
            result.Count.Should().Be(original.Count);
            result.Extras["color"].AsString.Should().Be("red");
            result.Extras["weight"].AsDouble.Should().Be(1.5);
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Inventory>();
            var reflection = BuildReflectionSerializer<Inventory>();

            SerializeUsing(generated, original).Should()
                .BeEquivalentTo(SerializeUsing(reflection, original));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Inventory>();
            var reflection = BuildReflectionSerializer<Inventory>();

            var bytesGen = SerializeUsing(generated, original);
            var bytesRefl = SerializeUsing(reflection, original);

            foreach (var (label, value) in new[]
                     {
                         ("gen->gen", DeserializeUsing(generated, bytesGen)),
                         ("gen->refl", DeserializeUsing(reflection, bytesGen)),
                         ("refl->gen", DeserializeUsing(generated, bytesRefl)),
                         ("refl->refl", DeserializeUsing(reflection, bytesRefl)),
                     })
            {
                value.Id.Should().Be(original.Id, label);
                value.Count.Should().Be(original.Count, label);
                value.Extras["color"].AsString.Should().Be("red", label);
                value.Extras["weight"].AsDouble.Should().Be(1.5, label);
            }
        }

        private static Inventory NewSample() => new Inventory
        {
            Id = ObjectId.GenerateNewId(),
            Count = 12,
            Extras = new BsonDocument
            {
                { "color", "red" },
                { "weight", 1.5 }
            }
        };
    }
}
