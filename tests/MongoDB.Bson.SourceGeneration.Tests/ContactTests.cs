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
    // Exercises the LookupSerializer fallback the emitter uses for non-primitive member types.
    // Both Contact and Address are in TestContext, so the lookup at runtime resolves to the
    // generated AddressSerializer rather than the reflection path.
    public class ContactTests
    {
        static ContactTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Nested_Address_Uses_Generated_Serializer()
        {
            var addressSerializer = BsonSerializer.LookupSerializer<Address>();
            addressSerializer.GetType().FullName.Should().StartWith("MongoDB.Bson.SourceGeneration.Tests.TestContext");
        }

        [Fact]
        public void Round_Trip_Preserves_Nested_Address()
        {
            var original = NewSample();
            var bytes = original.ToBson();
            var result = BsonSerializer.Deserialize<Contact>(bytes);

            result.Name.Should().Be(original.Name);
            result.HomeAddress.Should().NotBeNull();
            result.HomeAddress.Street.Should().Be(original.HomeAddress.Street);
            result.HomeAddress.City.Should().Be(original.HomeAddress.City);
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Contact>();
            var reflection = BuildReflectionSerializer<Contact>();

            SerializeUsing(generated, original).Should()
                .BeEquivalentTo(SerializeUsing(reflection, original));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Contact>();
            var reflection = BuildReflectionSerializer<Contact>();

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
                value.Name.Should().Be(original.Name, label);
                value.HomeAddress.Street.Should().Be(original.HomeAddress.Street, label);
                value.HomeAddress.City.Should().Be(original.HomeAddress.City, label);
            }
        }

        private static Contact NewSample() => new Contact
        {
            Id = ObjectId.GenerateNewId(),
            Name = "Ada Lovelace",
            HomeAddress = new Address { Street = "5 Wheatstone Way", City = "London" }
        };
    }
}
