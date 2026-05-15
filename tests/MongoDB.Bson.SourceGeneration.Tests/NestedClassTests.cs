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
    // Pins source-gen support for class-nested POCOs. Three shapes:
    //   - One-level nested public class.
    //   - Three-level nested (Outer.Middle.Inner) — exercises the multi-segment full-name path.
    //   - Same short-name from two different outer-class containers — should disambiguate to
    //     <T>Serializer / <T>2Serializer, same rule as namespace-based collisions.
    //
    // Roslyn's `INamedTypeSymbol.ToDisplayString(FullyQualifiedFormat)` produces the full
    // dotted chain (`global::Ns.Outer.Inner`) for nested types, so as long as the emit threads
    // that string through verbatim — which it does — there's nothing special to handle.
    public class NestedClassTests
    {
        static NestedClassTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void One_Level_Nested_Round_Trips_And_Matches_Reflection()
        {
            var sample = new OuterContainer.NestedPocoOne
            {
                Id = ObjectId.GenerateNewId(),
                Label = "Ada"
            };
            AssertByteIdenticalToReflection(sample);
            AssertFourWayCross(sample, (label, v) =>
            {
                v.Id.Should().Be(sample.Id, label);
                v.Label.Should().Be(sample.Label, label);
            });
        }

        [Fact]
        public void Three_Level_Nested_Round_Trips_And_Matches_Reflection()
        {
            var sample = new DeepOuter.Middle.DeeplyNestedPoco
            {
                Id = ObjectId.GenerateNewId(),
                Value = 42
            };
            AssertByteIdenticalToReflection(sample);
            AssertFourWayCross(sample, (label, v) =>
            {
                v.Id.Should().Be(sample.Id, label);
                v.Value.Should().Be(sample.Value, label);
            });
        }

        [Fact]
        public void Same_Short_Name_From_Different_Outer_Classes_Disambiguates()
        {
            // GeoBox.NestedMarker and SiteBox.NestedMarker share the short name "NestedMarker".
            // Disambiguator must rename the second one's emitted class to NestedMarker2Serializer
            // — and crucially, lookup by full name routes each instance to the right serializer
            // (otherwise we'd see the wrong subtype's bytes coming back).
            var geo = new GeoBox.NestedMarker { Label = "geo" };
            var site = new SiteBox.NestedMarker { Label = "site" };

            var geoBytes = geo.ToBson();
            var siteBytes = site.ToBson();

            BsonSerializer.Deserialize<GeoBox.NestedMarker>(geoBytes).Label.Should().Be("geo");
            BsonSerializer.Deserialize<SiteBox.NestedMarker>(siteBytes).Label.Should().Be("site");

            // Cross-check that the registered serializers are *different* instances — otherwise
            // disambiguation would have collapsed the two types onto one serializer class.
            var geoSerializer = BsonSerializer.LookupSerializer<GeoBox.NestedMarker>();
            var siteSerializer = BsonSerializer.LookupSerializer<SiteBox.NestedMarker>();
            geoSerializer.Should().NotBeSameAs(siteSerializer);
        }

        [Fact]
        public void Nested_Class_Lookup_Returns_Source_Gen_Serializer()
        {
            // Sanity: the source-gen provider claims the nested types, not the reflection
            // fallback. If the FullyQualifiedFormat conversion silently lost the containing-type
            // chain anywhere, the lookup would miss the provider and resolve to a reflection
            // BsonClassMapSerializer.
            var s = BsonSerializer.LookupSerializer<OuterContainer.NestedPocoOne>();
            s.GetType().Name.Should().Be("NestedPocoOneSerializer");
        }
    }
}
