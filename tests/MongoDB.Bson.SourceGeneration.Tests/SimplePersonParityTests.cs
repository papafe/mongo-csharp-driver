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
    // Four-way behavioral parity vs. BsonClassMapSerializer per §8 layer 2:
    // for each test POCO, serialize via generated + reflection-based serializers,
    // deserialize each output with each, and assert byte-identical wire format
    // plus equal in-memory values across the matrix.
    public class SimplePersonParityTests
    {
        static SimplePersonParityTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Generated_And_Reflection_Produce_Identical_Wire_Format()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<SimplePerson>();
            var reflection = BuildReflectionSerializer<SimplePerson>();

            var bytesGenerated = SerializeUsing(generated, original);
            var bytesReflection = SerializeUsing(reflection, original);

            bytesGenerated.Should().BeEquivalentTo(bytesReflection);
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<SimplePerson>();
            var reflection = BuildReflectionSerializer<SimplePerson>();

            var bytesGenerated = SerializeUsing(generated, original);
            var bytesReflection = SerializeUsing(reflection, original);

            var gg = DeserializeUsing(generated, bytesGenerated);
            var gr = DeserializeUsing(reflection, bytesGenerated);
            var rg = DeserializeUsing(generated, bytesReflection);
            var rr = DeserializeUsing(reflection, bytesReflection);

            foreach (var (label, value) in new[]
                     {
                         ("gen→gen", gg),
                         ("gen→refl", gr),
                         ("refl→gen", rg),
                         ("refl→refl", rr),
                     })
            {
                value.Id.Should().Be(original.Id, label);
                value.Name.Should().Be(original.Name, label);
                value.Age.Should().Be(original.Age, label);
            }
        }

        private static SimplePerson NewSample() => new SimplePerson
        {
            Id = ObjectId.GenerateNewId(),
            Name = "Ada Lovelace",
            Age = 36
        };
    }
}
