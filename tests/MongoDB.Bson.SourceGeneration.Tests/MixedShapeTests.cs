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
    public class MixedShapeTests
    {
        static MixedShapeTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Fields_Are_Emitted_Before_Properties()
        {
            var v = NewSample();
            var doc = BsonDocument.Parse(v.ToJson());

            // The runtime's ReadWriteMemberFinderConvention maps fields, then properties,
            // each in source order. So the wire layout is: FirstField, SecondField,
            // FirstProperty, SecondProperty.
            doc.ElementCount.Should().Be(4);
            doc.GetElement(0).Name.Should().Be("FirstField");
            doc.GetElement(1).Name.Should().Be("SecondField");
            doc.GetElement(2).Name.Should().Be("FirstProperty");
            doc.GetElement(3).Name.Should().Be("SecondProperty");
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var v = NewSample();
            var generated = BsonSerializer.LookupSerializer<MixedShape>();
            var reflection = BuildReflectionSerializer<MixedShape>();

            SerializeUsing(generated, v).Should()
                .BeEquivalentTo(SerializeUsing(reflection, v));
        }

        private static MixedShape NewSample() => new MixedShape
        {
            FirstField = 1,
            SecondField = 2,
            FirstProperty = "one",
            SecondProperty = "two"
        };
    }
}
