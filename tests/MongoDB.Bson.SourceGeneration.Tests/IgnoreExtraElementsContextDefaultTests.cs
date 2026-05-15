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
    // Exercises [BsonSourceGenerationOptions(IgnoreExtraElements = true)] as a context-wide default
    // and the per-type [BsonIgnoreExtraElements(false)] override. Behavioural assertion: when the
    // wire data contains an unknown element, deserialization either silently skips it (lenient) or
    // throws a BsonSerializationException (strict) according to the resolved option.
    public class IgnoreExtraElementsContextDefaultTests
    {
        static IgnoreExtraElementsContextDefaultTests()
        {
            IgnoreExtraElementsContext.Default.Register();
        }

        [Fact]
        public void Context_Default_Makes_Untagged_Type_Lenient()
        {
            var wire = new BsonDocument
            {
                { "Name", "Ada" },
                { "UnknownField", "ignored" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<LenientInherited>(wire);
            result.Name.Should().Be("Ada");
        }

        [Fact]
        public void Per_Type_BsonIgnoreExtraElements_False_Opts_Back_Into_Strict_Mode()
        {
            var wire = new BsonDocument
            {
                { "Name", "Ada" },
                { "UnknownField", "boom" }
            }.ToBson();

            // The per-type [BsonIgnoreExtraElements(false)] must beat the context-default lenient
            // setting. Without that beat, the wire would deserialise cleanly instead of throwing.
            Assert.Throws<BsonSerializationException>(() => BsonSerializer.Deserialize<StrictOptOut>(wire));
        }

        [Fact]
        public void Explicit_BsonIgnoreExtraElements_Still_Works_When_Context_Already_Lenient()
        {
            var wire = new BsonDocument
            {
                { "Name", "Ada" },
                { "UnknownField", "ignored" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<LenientExplicit>(wire);
            result.Name.Should().Be("Ada");
        }
    }
}
