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
    public class PersonRecordTests
    {
        static PersonRecordTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Positional_Record_Round_Trip()
        {
            var p = new PersonRecord("Ada", 36);
            var bytes = p.ToBson();
            var result = BsonSerializer.Deserialize<PersonRecord>(bytes);

            result.Name.Should().Be("Ada");
            result.Age.Should().Be(36);
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var p = new PersonRecord("Ada", 36);
            var generated = BsonSerializer.LookupSerializer<PersonRecord>();
            var reflection = BuildReflectionSerializer<PersonRecord>();

            SerializeUsing(generated, p).Should()
                .BeEquivalentTo(SerializeUsing(reflection, p));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = new PersonRecord("Ada", 36);
            var generated = BsonSerializer.LookupSerializer<PersonRecord>();
            var reflection = BuildReflectionSerializer<PersonRecord>();

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
                value.Name.Should().Be("Ada", label);
                value.Age.Should().Be(36, label);
            }
        }
    }
}
