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
    // Two per-member overrides that route the read/write through a cached serializer instance
    // instead of the inline primitive path:
    //   [BsonRepresentation(BsonType.X)]    — primitive re-encoded as a different BSON type.
    //   [BsonSerializer(typeof(MySerializer))] — caller-supplied serializer; generator just calls it.
    public class CustomSerializerTests
    {
        static CustomSerializerTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Representation_String_Encodes_Int32_As_BsonString()
        {
            var m = new Measurement { Id = ObjectId.GenerateNewId(), Count = 42, Magnitude = 1000 };
            var doc = BsonDocument.Parse(m.ToJson());

            doc["Count"].BsonType.Should().Be(BsonType.String);
            doc["Count"].AsString.Should().Be("42");
        }

        [Fact]
        public void Representation_String_Encodes_Int64_As_BsonString()
        {
            var m = new Measurement { Id = ObjectId.GenerateNewId(), Count = 42, Magnitude = 1000 };
            var doc = BsonDocument.Parse(m.ToJson());

            doc["Magnitude"].BsonType.Should().Be(BsonType.String);
            doc["Magnitude"].AsString.Should().Be("1000");
        }

        [Fact]
        public void Representation_String_Round_Trips_Numeric_Values()
        {
            var original = new Measurement { Id = ObjectId.GenerateNewId(), Count = 42, Magnitude = 1000 };
            var bytes = original.ToBson();
            var result = BsonSerializer.Deserialize<Measurement>(bytes);

            result.Count.Should().Be(42);
            result.Magnitude.Should().Be(1000);
        }

        [Fact]
        public void Custom_Serializer_Is_Invoked_On_Serialize()
        {
            var t = new Token { Id = ObjectId.GenerateNewId(), Code = "abc-123" };
            var doc = BsonDocument.Parse(t.ToJson());

            // UpperCaseStringSerializer uppercases the value, proving the routing.
            doc["Code"].AsString.Should().Be("ABC-123");
        }

        [Fact]
        public void Custom_Serializer_Is_Invoked_On_Deserialize()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Code", "abc-123" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<Token>(wire);
            result.Code.Should().Be("ABC-123");
        }
    }
}
