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

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // [BsonExtraElements] catch-all. Two supported member shapes:
    //   - BsonDocument                       — stores values as raw BsonValues.
    //   - IDictionary<string, object>        — values run through BsonTypeMapper, surface as .NET types.
    public class ExtraElementsTests
    {
        static ExtraElementsTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Unknown_Elements_Stashed_Into_BsonDocument_Catch_All()
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
        public void BsonDocument_Catch_All_Stays_Null_When_No_Unknown_Elements_Seen()
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
        public void BsonDocument_Catch_All_Round_Trips_Preserves_Values()
        {
            var original = new Inventory
            {
                Id = ObjectId.GenerateNewId(),
                Count = 12,
                Extras = new BsonDocument { { "color", "red" }, { "weight", 1.5 } }
            };
            var bytes = original.ToBson();
            var result = BsonSerializer.Deserialize<Inventory>(bytes);

            result.Id.Should().Be(original.Id);
            result.Count.Should().Be(original.Count);
            result.Extras["color"].AsString.Should().Be("red");
            result.Extras["weight"].AsDouble.Should().Be(1.5);
        }

        [Fact]
        public void Dictionary_Catch_All_Stashes_Unknown_Elements_As_DotNet_Values()
        {
            // Dictionary<string, object> path runs each value through BsonTypeMapper, so the
            // catch-all surfaces .NET types rather than BsonValues.
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Owner", "Ada" },
                { "tag", "alpha" },
                { "priority", 3 }
            }.ToBson();

            var result = BsonSerializer.Deserialize<TaggedRecord>(wire);

            result.Owner.Should().Be("Ada");
            result.Extras.Should().NotBeNull();
            result.Extras["tag"].Should().Be("alpha");
            result.Extras["priority"].Should().Be(3);
        }
    }
}
