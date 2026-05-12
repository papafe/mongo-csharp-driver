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
    public class TokenTests
    {
        static TokenTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Custom_Serializer_Is_Used_For_Write()
        {
            var t = new Token { Id = ObjectId.GenerateNewId(), Code = "abc-123" };
            var doc = BsonDocument.Parse(t.ToJson());

            doc["Code"].AsString.Should().Be("ABC-123");
        }

        [Fact]
        public void Custom_Serializer_Is_Used_For_Read()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Code", "abc-123" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<Token>(wire);
            result.Code.Should().Be("ABC-123");
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            // The reflection path also honors [BsonSerializer(typeof(X))], so a byte-identical
            // wire is what we expect — both routes go through UpperCaseStringSerializer.
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Token>();
            var reflection = BuildReflectionSerializer<Token>();

            SerializeUsing(generated, original).Should()
                .BeEquivalentTo(SerializeUsing(reflection, original));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Token>();
            var reflection = BuildReflectionSerializer<Token>();

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
                value.Code.Should().Be("ABC-123", label);
            }
        }

        private static Token NewSample() => new Token
        {
            Id = ObjectId.GenerateNewId(),
            Code = "abc-123"
        };
    }
}
