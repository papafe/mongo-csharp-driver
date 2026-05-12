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
    public class MeasurementTests
    {
        static MeasurementTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Int32_Member_Is_Written_As_String_When_Representation_Is_String()
        {
            var m = NewSample();
            var doc = BsonDocument.Parse(m.ToJson());

            doc["Count"].BsonType.Should().Be(BsonType.String);
            doc["Count"].AsString.Should().Be("42");
        }

        [Fact]
        public void Int64_Member_Is_Written_As_String_When_Representation_Is_String()
        {
            var m = NewSample();
            var doc = BsonDocument.Parse(m.ToJson());

            doc["Magnitude"].BsonType.Should().Be(BsonType.String);
            doc["Magnitude"].AsString.Should().Be("1000");
        }

        [Fact]
        public void String_Encoded_Numbers_Round_Trip()
        {
            var original = NewSample();
            var bytes = original.ToBson();
            var result = BsonSerializer.Deserialize<Measurement>(bytes);

            result.Count.Should().Be(42);
            result.Magnitude.Should().Be(1000);
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Measurement>();
            var reflection = BuildReflectionSerializer<Measurement>();

            SerializeUsing(generated, original).Should()
                .BeEquivalentTo(SerializeUsing(reflection, original));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Measurement>();
            var reflection = BuildReflectionSerializer<Measurement>();

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
                value.Id.Should().Be(original.Id, label);
                value.Count.Should().Be(original.Count, label);
                value.Magnitude.Should().Be(original.Magnitude, label);
            }
        }

        private static Measurement NewSample() => new Measurement
        {
            Id = ObjectId.GenerateNewId(),
            Count = 42,
            Magnitude = 1000
        };
    }
}
