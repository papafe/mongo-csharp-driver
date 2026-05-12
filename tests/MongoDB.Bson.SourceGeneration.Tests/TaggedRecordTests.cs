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

using System.Collections.Generic;
using FluentAssertions;
using MongoDB.Bson.Serialization;
using Xunit;
using static MongoDB.Bson.SourceGeneration.Tests.SerializationTestHelpers;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Exercises the Dictionary<string, object> shape of [BsonExtraElements] — the runtime
    // marshals each value through BsonTypeMapper, so the catch-all sees .NET types, not BsonValues.
    public class TaggedRecordTests
    {
        static TaggedRecordTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Unknown_Elements_Stashed_As_DotNet_Values()
        {
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

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<TaggedRecord>();
            var reflection = BuildReflectionSerializer<TaggedRecord>();

            SerializeUsing(generated, original).Should()
                .BeEquivalentTo(SerializeUsing(reflection, original));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<TaggedRecord>();
            var reflection = BuildReflectionSerializer<TaggedRecord>();

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
                value.Owner.Should().Be(original.Owner, label);
                value.Extras["tag"].Should().Be("beta", label);
                value.Extras["priority"].Should().Be(9, label);
            }
        }

        private static TaggedRecord NewSample() => new TaggedRecord
        {
            Id = ObjectId.GenerateNewId(),
            Owner = "Grace",
            Extras = new Dictionary<string, object>
            {
                { "tag", "beta" },
                { "priority", 9 }
            }
        };
    }
}
