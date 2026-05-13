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
    // Non-primitive nested members: the generated serializer can't read/write them directly, so
    // it falls back to BsonSerializer.LookupSerializer<TMember> at runtime. When the member's
    // type is also in the context, that lookup resolves to the generated serializer too — staying
    // on the source-gen path end-to-end.
    public class NestedTypeTests
    {
        static NestedTypeTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Nested_Member_Lookup_Resolves_To_Generated_Serializer()
        {
            var addressSerializer = BsonSerializer.LookupSerializer<Address>();
            addressSerializer.GetType().FullName.Should().StartWith("MongoDB.Bson.SourceGeneration.Tests.TestContext");
        }

        [Fact]
        public void Nested_Member_Round_Trips_Through_Both_Serializers()
        {
            var original = new Contact
            {
                Id = ObjectId.GenerateNewId(),
                Name = "Ada Lovelace",
                HomeAddress = new Address { Street = "5 Wheatstone Way", City = "London" }
            };

            var bytes = original.ToBson();
            var result = BsonSerializer.Deserialize<Contact>(bytes);

            result.Name.Should().Be(original.Name);
            result.HomeAddress.Should().NotBeNull();
            result.HomeAddress.Street.Should().Be(original.HomeAddress.Street);
            result.HomeAddress.City.Should().Be(original.HomeAddress.City);
        }
    }
}
