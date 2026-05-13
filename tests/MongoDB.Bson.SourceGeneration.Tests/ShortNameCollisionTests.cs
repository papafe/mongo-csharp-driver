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
    // Geo.Marker and Site.Marker share a CLR short name. The extractor's disambiguation pass
    // renames the second occurrence (MarkerSerializer2 / s_marker2Serializer), so the partial
    // context compiles and the registry hands out distinct serializers for each fully-qualified
    // type. Without the disambiguation, the build would fail with CS0102.
    public class ShortNameCollisionTests
    {
        static ShortNameCollisionTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Colliding_Short_Names_Resolve_To_Distinct_Serializers()
        {
            var geoSerializer = BsonSerializer.LookupSerializer<Geo.Marker>();
            var siteSerializer = BsonSerializer.LookupSerializer<Site.Marker>();

            geoSerializer.GetType().Should().NotBe(siteSerializer.GetType());
        }

        [Fact]
        public void Colliding_Short_Names_Round_Trip_Independently()
        {
            var geo = new Geo.Marker { Value = 3.14 };
            var site = new Site.Marker { Tag = "alpha" };

            var rg = BsonSerializer.Deserialize<Geo.Marker>(geo.ToBson());
            var rs = BsonSerializer.Deserialize<Site.Marker>(site.ToBson());

            rg.Value.Should().Be(3.14);
            rs.Tag.Should().Be("alpha");
        }

        // The direct-call optimization routes each nested member to its in-context cached field.
        // When two types share a short name, the lookup must go by full name so the correctly-
        // disambiguated serializer ends up in the emit. Round-tripping a POCO that holds both
        // confirms the routing — if the wrong serializer were picked, the *other* member would
        // round-trip as the wrong type (or fail to compile in the first place).
        [Fact]
        public void Member_Of_Each_Colliding_Type_Is_Routed_To_Correct_Serializer()
        {
            var p = new MarkerPair
            {
                GeoMarker = new Geo.Marker { Value = 2.5 },
                SiteMarker = new Site.Marker { Tag = "beta" }
            };

            var result = BsonSerializer.Deserialize<MarkerPair>(p.ToBson());

            result.GeoMarker.Value.Should().Be(2.5);
            result.SiteMarker.Tag.Should().Be("beta");
        }
    }
}
