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
    // End-to-end sanity: the generator runs, the registry hands out the generated serializer
    // ahead of the reflection-based provider, and the emitted wire format matches a canonical
    // BsonDocument with the same field order.
    public class RoundTripTests
    {
        static RoundTripTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void RoundTrip_Via_BsonSerializer_Preserves_All_Fields()
        {
            var original = new SimplePerson
            {
                Id = ObjectId.GenerateNewId(),
                Name = "Ada Lovelace",
                Age = 36
            };

            var bytes = original.ToBson();
            var deserialized = BsonSerializer.Deserialize<SimplePerson>(bytes);

            deserialized.Id.Should().Be(original.Id);
            deserialized.Name.Should().Be(original.Name);
            deserialized.Age.Should().Be(original.Age);
        }

        [Fact]
        public void Generated_Serializer_Is_Used_Not_Reflection()
        {
            var serializer = BsonSerializer.LookupSerializer<SimplePerson>();
            serializer.GetType().Name.Should().Be("SimplePersonSerializer");
        }

        [Fact]
        public void Wire_Format_Matches_Canonical_BsonDocument()
        {
            var id = ObjectId.GenerateNewId();
            var person = new SimplePerson { Id = id, Name = "Ada Lovelace", Age = 36 };

            var fromGenerated = person.ToBson();
            var fromDocument = new BsonDocument
            {
                { "_id", id },
                { "Name", "Ada Lovelace" },
                { "Age", 36 }
            }.ToBson();

            fromGenerated.Should().BeEquivalentTo(fromDocument);
        }
    }
}
