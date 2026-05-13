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
    // What counts as a member, and in what order: public fields are first-class members, and
    // mixed-shape types emit fields before properties to match the runtime's
    // ReadWriteMemberFinderConvention.
    public class MemberDiscoveryTests
    {
        static MemberDiscoveryTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Public_Fields_Round_Trip_Like_Properties()
        {
            var original = new Coordinates { Latitude = 51.4934, Longitude = 0.0098, Label = "Greenwich" };
            var bytes = original.ToBson();
            var result = BsonSerializer.Deserialize<Coordinates>(bytes);

            result.Latitude.Should().Be(original.Latitude);
            result.Longitude.Should().Be(original.Longitude);
            result.Label.Should().Be(original.Label);
        }

        [Fact]
        public void Fields_Are_Emitted_Before_Properties()
        {
            var v = new MixedShape { FirstField = 1, SecondField = 2, FirstProperty = "one", SecondProperty = "two" };
            var doc = BsonDocument.Parse(v.ToJson());

            // ReadWriteMemberFinderConvention maps fields first, then properties, each in source
            // order — wire layout: FirstField, SecondField, FirstProperty, SecondProperty.
            doc.ElementCount.Should().Be(4);
            doc.GetElement(0).Name.Should().Be("FirstField");
            doc.GetElement(1).Name.Should().Be("SecondField");
            doc.GetElement(2).Name.Should().Be("FirstProperty");
            doc.GetElement(3).Name.Should().Be("SecondProperty");
        }
    }
}
