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

using System;
using System.IO;
using FluentAssertions;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using Xunit;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Exercises [BsonSourceGenerationOptions(DefaultGuidRepresentation = ...)] across all four
    // layers of override. Behavioural assertion (the only meaningful one): a Guid serialised via
    // source-gen produces the *exact same bytes* as a hand-constructed GuidSerializer with the
    // expected representation. If the generator picked the wrong representation at any layer of
    // the fold, the bytes would differ.
    public class GuidRepresentationTests
    {
        static GuidRepresentationTests()
        {
            GuidTestContext.Default.Register();
        }

        // A fixed Guid so byte comparisons are deterministic.
        private static readonly Guid SampleGuid = new("01234567-89ab-cdef-0123-456789abcdef");

        [Fact]
        public void Context_Default_Bakes_The_Standard_Representation()
            => AssertGuidEncodedAs(new WithGuid { Value = SampleGuid }, GuidRepresentation.Standard);

        [Fact]
        public void Per_Listing_Override_Wins_Over_Context_Default()
            => AssertGuidEncodedAs(new WithGuidPerListingOverride { Value = SampleGuid }, GuidRepresentation.JavaLegacy);

        [Fact]
        public void Per_Poco_Override_Wins_Over_Context_Default()
            => AssertGuidEncodedAs(new WithGuidPerPocoOverride { Value = SampleGuid }, GuidRepresentation.PythonLegacy);

        [Fact]
        public void Per_Member_BsonRepresentation_Wins_Over_All_Defaults()
        {
            // WithGuidExplicit has [BsonRepresentation(BsonType.String)] on Id — explicit per-member
            // attribute overrides every context-level default. The wire shape is a BSON string, not
            // a binary.
            var v = new WithGuidExplicit { Value = SampleGuid };
            var bytes = v.ToBson();
            var doc = global::MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(bytes);

            doc["Value"].BsonType.Should().Be(BsonType.String);
            doc["Value"].AsString.Should().Be(SampleGuid.ToString());
        }

        [Fact]
        public void All_Variants_Round_Trip_Their_Guid_Values()
        {
            // Round-tripping through source-gen preserves the Guid regardless of representation.
            var a = new WithGuid { Value = SampleGuid };
            var b = new WithGuidPerListingOverride { Value = SampleGuid };
            var c = new WithGuidPerPocoOverride { Value = SampleGuid };
            var d = new WithGuidExplicit { Value = SampleGuid };
            var e = new WithGuidPerMemberAttribute { Value = SampleGuid };

            BsonSerializer.Deserialize<WithGuid>(a.ToBson()).Value.Should().Be(SampleGuid);
            BsonSerializer.Deserialize<WithGuidPerListingOverride>(b.ToBson()).Value.Should().Be(SampleGuid);
            BsonSerializer.Deserialize<WithGuidPerPocoOverride>(c.ToBson()).Value.Should().Be(SampleGuid);
            BsonSerializer.Deserialize<WithGuidExplicit>(d.ToBson()).Value.Should().Be(SampleGuid);
            BsonSerializer.Deserialize<WithGuidPerMemberAttribute>(e.ToBson()).Value.Should().Be(SampleGuid);
        }

        [Fact]
        public void Per_Member_BsonGuidRepresentation_Wins_Over_Context_Default()
        {
            // The context default is Standard (set on GuidTestContext). The per-member
            // [BsonGuidRepresentation(PythonLegacy)] on WithGuidPerMemberAttribute.Value must
            // override it. Pre-fix, the attribute was silently dropped and the Guid would have
            // gone out with the Standard byte order.
            AssertGuidEncodedAs(new WithGuidPerMemberAttribute { Value = SampleGuid }, GuidRepresentation.PythonLegacy);
        }

        // Encodes the POCO via source-gen, extracts the BSON binary value of the Value field, and
        // compares it byte-for-byte to a hand-constructed binary with the expected representation.
        // If the generator baked the wrong representation, the binary contents (byte order /
        // subtype) would differ.
        private static void AssertGuidEncodedAs<T>(T value, GuidRepresentation expected)
        {
            var bytes = SerializeRoot(value);
            var doc = global::MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(bytes);
            var actualBinary = doc["Value"].AsBsonBinaryData;

            var expectedBinary = ConvertGuidToExpectedBinary(SampleGuid, expected);

            actualBinary.SubType.Should().Be(expectedBinary.SubType, $"expected {expected} subtype");
            actualBinary.Bytes.Should().Equal(expectedBinary.Bytes, $"expected {expected} byte order");
        }

        private static byte[] SerializeRoot<T>(T value)
        {
            using var stream = new MemoryStream();
            using (var writer = new BsonBinaryWriter(stream))
            {
                BsonSerializer.Serialize(writer, value);
            }
            return stream.ToArray();
        }

        private static BsonBinaryData ConvertGuidToExpectedBinary(Guid g, GuidRepresentation representation)
        {
            // Use the same routine the runtime uses to encode a Guid as a BSON binary.
            return GuidConverter.ToBytes(g, representation) is var raw
                ? new BsonBinaryData(raw, representation == GuidRepresentation.Standard
                    ? BsonBinarySubType.UuidStandard
                    : BsonBinarySubType.UuidLegacy)
                : throw new InvalidOperationException();
        }
    }
}
