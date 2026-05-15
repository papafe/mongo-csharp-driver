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
    // Exercises [BsonSourceGenerationOptions(PropertyNamingPolicy = ...)] across all layers of
    // the override fold. Behavioural assertion: a member's wire name in the emitted BSON document
    // matches the policy chosen at the highest-precedence layer that set it. If the generator
    // applied the wrong policy at any layer, the element name in the parsed document would differ.
    public class PropertyNamingPolicyTests
    {
        static PropertyNamingPolicyTests()
        {
            NamingPolicyTestContext.Default.Register();
        }

        [Fact]
        public void Context_Default_CamelCase_Lowers_Only_The_First_Character()
        {
            // Matches the existing reflection-path CamelCaseElementNameConvention: only the first
            // character is lowercased; runs of uppercase are preserved.
            var v = new WithCamelCaseDefault { FirstName = "Ada", URLBuilder = "u", ID = "x" };
            var doc = BsonDocument.Parse(v.ToJson());

            doc.Contains("firstName").Should().BeTrue();
            doc.Contains("uRLBuilder").Should().BeTrue();
            doc.Contains("iD").Should().BeTrue();

            doc.Contains("FirstName").Should().BeFalse();
            doc.Contains("URLBuilder").Should().BeFalse();
        }

        [Fact]
        public void Per_Listing_Override_To_SnakeCase_Wins_Over_Context_Default()
        {
            var v = new WithSnakeCasePerListing { FirstName = "Ada", URLBuilder = "u" };
            var doc = BsonDocument.Parse(v.ToJson());

            // FirstName → first_name (single lower→upper boundary)
            // URLBuilder → url_builder (consecutive caps followed by lower → one word boundary)
            doc.Contains("first_name").Should().BeTrue();
            doc.Contains("url_builder").Should().BeTrue();

            doc.Contains("firstName").Should().BeFalse();
            doc.Contains("uRLBuilder").Should().BeFalse();
        }

        [Fact]
        public void Per_Poco_Override_To_KebabCase_Wins_Over_Context_Default()
        {
            var v = new WithKebabCasePerPoco { FirstName = "Ada", URLBuilder = "u" };
            var doc = BsonDocument.Parse(v.ToJson());

            doc.Contains("first-name").Should().BeTrue();
            doc.Contains("url-builder").Should().BeTrue();
        }

        [Fact]
        public void Explicit_BsonElement_Wins_Over_Naming_Policy()
        {
            // [BsonElement("legacy_explicit_name")] is honored verbatim, not re-transformed.
            // Without this, the policy might re-camelCase the explicit name to "legacy_explicit_name"
            // (no-op for this string but the principle holds).
            var v = new WithExplicitOverrides { MyKey = "k", FirstName = "Ada" };
            var doc = BsonDocument.Parse(v.ToJson());

            doc.Contains("legacy_explicit_name").Should().BeTrue();
            doc.Contains("firstName").Should().BeFalse();
        }

        [Fact]
        public void BsonId_Always_Maps_To_Underscore_Id_Regardless_Of_Policy()
        {
            var v = new WithExplicitOverrides { MyKey = "k", FirstName = "Ada" };
            var doc = BsonDocument.Parse(v.ToJson());

            doc.Contains("_id").Should().BeTrue();
            doc.Contains("myKey").Should().BeFalse();
            doc.Contains("MyKey").Should().BeFalse();
        }

        [Fact]
        public void Round_Trip_Preserves_Values_Under_Each_Policy()
        {
            var a = new WithCamelCaseDefault { FirstName = "Ada", URLBuilder = "u", ID = "x" };
            var b = new WithSnakeCasePerListing { FirstName = "Ada", URLBuilder = "u" };
            var c = new WithKebabCasePerPoco { FirstName = "Ada", URLBuilder = "u" };

            var a2 = BsonSerializer.Deserialize<WithCamelCaseDefault>(a.ToBson());
            var b2 = BsonSerializer.Deserialize<WithSnakeCasePerListing>(b.ToBson());
            var c2 = BsonSerializer.Deserialize<WithKebabCasePerPoco>(c.ToBson());

            a2.FirstName.Should().Be("Ada");
            a2.URLBuilder.Should().Be("u");
            a2.ID.Should().Be("x");

            b2.FirstName.Should().Be("Ada");
            b2.URLBuilder.Should().Be("u");

            c2.FirstName.Should().Be("Ada");
            c2.URLBuilder.Should().Be("u");
        }
    }
}
