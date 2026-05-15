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
    // Parity for `decimal` and `float` members. Before these CLR types were added to PrimitiveKind
    // they fell through to LookupSerializer<T>() — functionally correct, but [BsonRepresentation]
    // on them was silently ignored because the per-member-override emit path only fires for kinds
    // it knows how to construct. They now route through cached DecimalSerializer / SingleSerializer
    // instances, matching the reflection path for both default and overridden representations.
    public class DecimalAndFloatTests
    {
        static DecimalAndFloatTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Bare_Decimal_And_Float_Are_Byte_Identical_To_Reflection()
        {
            var sample = new MoneyEntry
            {
                Id = ObjectId.GenerateNewId(),
                Amount = 12.34m,
                Ratio = 1.5f
            };
            AssertByteIdenticalToReflection(sample);
        }

        [Fact]
        public void Bare_Decimal_Default_Representation_Is_Decimal128()
        {
            // DecimalSerializer's parameterless ctor defaults to BsonType.Decimal128 — see
            // DecimalSerializer.cs:44-47. The cached `new DecimalSerializer()` field inherits that.
            var v = new MoneyEntry { Id = ObjectId.GenerateNewId(), Amount = 12.34m, Ratio = 0f };
            var doc = BsonDocument.Parse(v.ToJson());

            doc["Amount"].BsonType.Should().Be(BsonType.Decimal128);
        }

        [Fact]
        public void Bare_Float_Default_Representation_Is_Double()
        {
            // SingleSerializer's parameterless ctor defaults to BsonType.Double.
            var v = new MoneyEntry { Id = ObjectId.GenerateNewId(), Amount = 0m, Ratio = 1.5f };
            var doc = BsonDocument.Parse(v.ToJson());

            doc["Ratio"].BsonType.Should().Be(BsonType.Double);
        }

        [Fact]
        public void BsonRepresentation_String_On_Decimal_Is_Honored()
        {
            // Pre-fix, this assertion would fail: the attribute was silently ignored because
            // decimal wasn't classified as PrimitiveKind. Now [BsonRepresentation(BsonType.String)]
            // caches `new DecimalSerializer(BsonType.String)` and routes through it.
            var v = new MoneyEntryAsString
            {
                Id = ObjectId.GenerateNewId(),
                Amount = 99.99m,
                Ratio = 0f
            };
            var doc = BsonDocument.Parse(v.ToJson());

            doc["Amount"].BsonType.Should().Be(BsonType.String);
            doc["Amount"].AsString.Should().Be("99.99");
        }

        [Fact]
        public void BsonRepresentation_String_On_Float_Is_Honored()
        {
            var v = new MoneyEntryAsString
            {
                Id = ObjectId.GenerateNewId(),
                Amount = 0m,
                Ratio = 1.5f
            };
            var doc = BsonDocument.Parse(v.ToJson());

            doc["Ratio"].BsonType.Should().Be(BsonType.String);
            doc["Ratio"].AsString.Should().Be("1.5");
        }

        [Fact]
        public void BsonRepresentation_Output_Is_Byte_Identical_To_Reflection()
        {
            var sample = new MoneyEntryAsString
            {
                Id = ObjectId.GenerateNewId(),
                Amount = 99.99m,
                Ratio = 1.5f
            };
            AssertByteIdenticalToReflection(sample);
        }

        [Fact]
        public void Round_Trip_Preserves_Values_For_Both_Shapes()
        {
            AssertFourWayCross(
                new MoneyEntry { Id = ObjectId.GenerateNewId(), Amount = 12.34m, Ratio = 1.5f },
                (label, v) =>
                {
                    v.Amount.Should().Be(12.34m, label);
                    v.Ratio.Should().Be(1.5f, label);
                });

            AssertFourWayCross(
                new MoneyEntryAsString { Id = ObjectId.GenerateNewId(), Amount = 99.99m, Ratio = 1.5f },
                (label, v) =>
                {
                    v.Amount.Should().Be(99.99m, label);
                    v.Ratio.Should().Be(1.5f, label);
                });
        }

        [Fact]
        public void Source_Gen_Coerces_Int32_To_Decimal_And_Float()
        {
            // Just like Int32Serializer, DecimalSerializer / SingleSerializer accept multiple
            // wire types and coerce. Source-gen now inherits that behaviour because it routes
            // through the cached serializer instance.
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Amount", 5 },     // Int32 wire, decimal CLR
                { "Ratio", 7 }       // Int32 wire, float CLR
            }.ToBson();

            var v = BsonSerializer.Deserialize<MoneyEntry>(wire);

            v.Amount.Should().Be(5m);
            v.Ratio.Should().Be(7f);
        }
    }
}
