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
    // Exercises the public-field path. The runtime's default member-finder convention
    // also picks up public read/write fields, so generated and reflection should agree.
    public class CoordinatesTests
    {
        static CoordinatesTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Round_Trip_Preserves_Field_Values()
        {
            var original = NewSample();
            var bytes = original.ToBson();
            var deserialized = BsonSerializer.Deserialize<Coordinates>(bytes);

            deserialized.Latitude.Should().Be(original.Latitude);
            deserialized.Longitude.Should().Be(original.Longitude);
            deserialized.Label.Should().Be(original.Label);
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Coordinates>();
            var reflection = BuildReflectionSerializer<Coordinates>();

            SerializeUsing(generated, original).Should()
                .BeEquivalentTo(SerializeUsing(reflection, original));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Coordinates>();
            var reflection = BuildReflectionSerializer<Coordinates>();

            var bytesGen = SerializeUsing(generated, original);
            var bytesRefl = SerializeUsing(reflection, original);

            foreach (var (label, value) in new[]
                     {
                         ("gen→gen", DeserializeUsing(generated, bytesGen)),
                         ("gen→refl", DeserializeUsing(reflection, bytesGen)),
                         ("refl→gen", DeserializeUsing(generated, bytesRefl)),
                         ("refl→refl", DeserializeUsing(reflection, bytesRefl)),
                     })
            {
                value.Latitude.Should().Be(original.Latitude, label);
                value.Longitude.Should().Be(original.Longitude, label);
                value.Label.Should().Be(original.Label, label);
            }
        }

        private static Coordinates NewSample() => new Coordinates
        {
            Latitude = 51.4934,
            Longitude = 0.0098,
            Label = "Greenwich"
        };
    }
}
