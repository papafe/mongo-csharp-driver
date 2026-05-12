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
    public class AccountProfileTests
    {
        static AccountProfileTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Init_Only_Round_Trip()
        {
            var p = new AccountProfile { Id = ObjectId.GenerateNewId(), DisplayName = "Ada", Bio = "Mathematician" };
            var bytes = p.ToBson();
            var result = BsonSerializer.Deserialize<AccountProfile>(bytes);

            result.Id.Should().Be(p.Id);
            result.DisplayName.Should().Be("Ada");
            result.Bio.Should().Be("Mathematician");
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var p = new AccountProfile { Id = ObjectId.GenerateNewId(), DisplayName = "Ada", Bio = "Mathematician" };
            var generated = BsonSerializer.LookupSerializer<AccountProfile>();
            var reflection = BuildReflectionSerializer<AccountProfile>();

            SerializeUsing(generated, p).Should()
                .BeEquivalentTo(SerializeUsing(reflection, p));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = new AccountProfile { Id = ObjectId.GenerateNewId(), DisplayName = "Ada", Bio = "Mathematician" };
            var generated = BsonSerializer.LookupSerializer<AccountProfile>();
            var reflection = BuildReflectionSerializer<AccountProfile>();

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
                value.DisplayName.Should().Be("Ada", label);
                value.Bio.Should().Be("Mathematician", label);
            }
        }
    }
}
