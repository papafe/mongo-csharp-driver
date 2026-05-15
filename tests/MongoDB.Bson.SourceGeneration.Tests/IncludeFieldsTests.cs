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
    // Exercises [BsonSourceGenerationOptions(IncludeFields = false)] — the context-wide opt-out
    // of public fields. Behavioural assertions:
    //   - On serialize, the field is absent from the emitted document.
    //   - On deserialize, the field is silently ignored (treated as an unknown element). The
    //     context doesn't enable IgnoreExtraElements, so an unknown wire element still throws;
    //     a field that's *absent* from the wire stays at default.
    public class IncludeFieldsTests
    {
        static IncludeFieldsTests()
        {
            IncludeFieldsContext.Default.Register();
        }

        [Fact]
        public void Field_Is_Excluded_From_Emitted_Document()
        {
            var v = new WithFieldsAndProperties { PropName = "prop", FieldName = "field" };
            var doc = BsonDocument.Parse(v.ToJson());

            doc.Contains("PropName").Should().BeTrue();
            doc.Contains("FieldName").Should().BeFalse();
        }

        [Fact]
        public void Round_Trip_Preserves_Property_Drops_Field()
        {
            // FieldName goes in but doesn't come out — the generator dropped it from both
            // serialize and deserialize halves of the emit.
            var v = new WithFieldsAndProperties { PropName = "prop", FieldName = "field" };
            var bytes = v.ToBson();
            var result = BsonSerializer.Deserialize<WithFieldsAndProperties>(bytes);

            result.PropName.Should().Be("prop");
            result.FieldName.Should().BeNull();
        }

        [Fact]
        public void Default_Context_Without_IncludeFields_Still_Includes_Fields()
        {
            // Sanity: the default Coordinates POCO (in TestContext) still has all three of its
            // public fields in the wire output. Regression guard against the option leaking
            // across contexts.
            TestContext.Default.Register();
            var c = new Coordinates { Latitude = 1.0, Longitude = 2.0, Label = "lab" };
            var doc = BsonDocument.Parse(c.ToJson());

            doc.Contains("Latitude").Should().BeTrue();
            doc.Contains("Longitude").Should().BeTrue();
            doc.Contains("Label").Should().BeTrue();
        }
    }
}
