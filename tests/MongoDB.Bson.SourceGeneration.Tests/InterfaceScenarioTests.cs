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
    // Pins what works with interfaces. Reflection is the reference: we want byte parity wherever
    // possible. Scenarios:
    //   - Interface-typed collection member (`IList<int>`, `IDictionary<string,int>`): the emit
    //     falls through to BsonSerializer.LookupSerializer<TMember>, which the runtime resolves to
    //     the appropriate collection serializer. AOT-imperfect (registry trip) but functionally
    //     correct.
    //   - Record implementing an interface: the interface is irrelevant to source-gen; the record
    //     emits normally.
    public class InterfaceScenarioTests
    {
        static InterfaceScenarioTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Interface_Typed_IList_Member_Round_Trips()
        {
            var original = new WithCollections
            {
                Numbers = new List<int> { 1, 2, 3 },
                Counts = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }
            };

            var bytes = original.ToBson();
            var result = BsonSerializer.Deserialize<WithCollections>(bytes);

            result.Numbers.Should().Equal(1, 2, 3);
            result.Counts.Should().HaveCount(2);
            result.Counts["a"].Should().Be(1);
            result.Counts["b"].Should().Be(2);
        }

        [Fact]
        public void Interface_Typed_Collection_Members_Match_Reflection_Wire_Format()
            => AssertByteIdenticalToReflection(new WithCollections
            {
                Numbers = new List<int> { 1, 2, 3 },
                Counts = new Dictionary<string, int> { { "a", 1 } }
            });

        [Fact]
        public void Record_Implementing_Interface_Round_Trips()
        {
            var w = new Widget("gizmo", 7);
            var bytes = w.ToBson();
            var result = BsonSerializer.Deserialize<Widget>(bytes);

            result.Label.Should().Be("gizmo");
            result.Quantity.Should().Be(7);
        }

        [Fact]
        public void Record_Implementing_Interface_Matches_Reflection_Wire_Format()
            => AssertByteIdenticalToReflection(new Widget("gizmo", 7));

        // Listing an interface directly in [BsonSerializable] is allowed and produces a
        // dispatch-only serializer (the abstract-type emit path covers interfaces because
        // INamedTypeSymbol.IsAbstract is true for them). End-to-end usefulness depends on the
        // user wiring concrete-subtype discriminators through the runtime — source-gen can't
        // bake known-types dispatch because [BsonKnownTypes] / [BsonDiscriminator] target
        // Class | Struct, not Interface. These tests pin the locally-observable behaviour: the
        // emit exists, case-null and case-self both throw, and the type is registered.
        [Fact]
        public void Interface_Listed_In_Context_Emits_A_Serializer()
        {
            var serializer = BsonSerializer.LookupSerializer<IThing>();

            serializer.Should().NotBeNull();
            serializer.GetType().Name.Should().Be("IThingSerializer");
        }

        [Fact]
        public void Interface_Deserialize_Without_Discriminator_Throws()
        {
            var wire = new BsonDocument { { "Label", "x" } }.ToBson();

            var ex = Assert.Throws<BsonSerializationException>(
                () => BsonSerializer.Deserialize<IThing>(wire));
            ex.Message.Should().Contain("abstract").And.Contain("IThing");
        }

        [Fact]
        public void Interface_Deserialize_With_Self_Discriminator_Throws()
        {
            var wire = new BsonDocument
            {
                { "_t", "IThing" },
                { "Label", "x" }
            }.ToBson();

            Assert.Throws<BsonSerializationException>(
                () => BsonSerializer.Deserialize<IThing>(wire));
        }
    }
}
