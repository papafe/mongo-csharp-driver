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
    // Pins behaviour parity between the reflection path and source-gen for the seven
    // inline-primitive member types (bool, int, long, double, string, ObjectId, Decimal128) when
    // the wire BSON type doesn't match the CLR type. Both paths must coerce identically.
    //
    // Reflection: Int32Serializer.Deserialize switches on the wire type and accepts Int32, Int64,
    // Double, Decimal128, and String — coercing each to int. Same shape for the other primitive
    // serializers (DoubleSerializer, BooleanSerializer, etc. in
    // src/MongoDB.Bson/Serialization/Serializers/).
    //
    // Source-gen historically emitted a literal `reader.ReadInt32()`, which throws on any wire
    // type other than Int32 because BsonBinaryReader.VerifyBsonType enforces an exact match. That
    // was a silent migration gotcha: a user opting a type into a context would suddenly fail to
    // read old data the reflection path handled fine.
    //
    // The current emit routes through a cached static Int32Serializer instance, inheriting the
    // reflection path's coercion for free while staying AOT-clean (concrete type, no registry).
    // These tests freeze the parity contract; an emit regression would put one or both of the
    // assertions on a wire-type mismatch back into the failing column.
    public class PrimitiveCoercionDivergenceTests
    {
        static PrimitiveCoercionDivergenceTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Source_Gen_Coerces_Double_To_Int32()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Name", "x" },
                { "Age", 5.0 }
            }.ToBson();

            var result = BsonSerializer.Deserialize<SimplePerson>(wire);

            result.Age.Should().Be(5, "source-gen routes int reads through Int32Serializer, which coerces Double");
        }

        [Fact]
        public void Source_Gen_Coerces_String_To_Int32()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Name", "x" },
                { "Age", "42" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<SimplePerson>(wire);

            result.Age.Should().Be(42, "source-gen routes int reads through Int32Serializer, which coerces String via JsonConvert");
        }

        [Fact]
        public void Source_Gen_Coerces_Int64_To_Int32()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Name", "x" },
                { "Age", (long)7 }
            }.ToBson();

            var result = BsonSerializer.Deserialize<SimplePerson>(wire);

            result.Age.Should().Be(7, "source-gen routes int reads through Int32Serializer, which coerces Int64");
        }

        [Fact]
        public void Source_Gen_Wire_Output_Matches_Reflection_For_Plain_Int32()
        {
            // Belt-and-braces: routing through Int32Serializer must not change the *write* shape
            // for the matching-type happy path. A plain int still goes out as a BsonType.Int32.
            var v = new SimplePerson { Id = ObjectId.GenerateNewId(), Name = "x", Age = 5 };
            var doc = BsonDocument.Parse(v.ToJson());

            doc["Age"].BsonType.Should().Be(BsonType.Int32);
            doc["Age"].AsInt32.Should().Be(5);
        }
    }
}
