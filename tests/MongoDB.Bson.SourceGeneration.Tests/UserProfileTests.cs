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
    // Verifies batch 1 of ticket #3: [BsonIgnore], [BsonIgnoreExtraElements], [BsonNoId].
    public class UserProfileTests
    {
        static UserProfileTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void NoId_Suppresses_Id_To_Underscore_Id_Mapping()
        {
            var profile = NewSample();
            var doc = BsonDocument.Parse(profile.ToJson());

            doc.Contains("Id").Should().BeTrue("with [BsonNoId] the member stays as 'Id'");
            doc.Contains("_id").Should().BeFalse();
        }

        [Fact]
        public void BsonIgnore_Drops_Member_From_Output()
        {
            var profile = NewSample();
            profile.InternalToken = "secret";

            var doc = BsonDocument.Parse(profile.ToJson());

            doc.Contains("InternalToken").Should().BeFalse();
        }

        [Fact]
        public void IgnoreExtraElements_Allows_Unknown_Elements_On_Deserialize()
        {
            // Wire BSON with an extra "ExtraField" not on UserProfile.
            var wire = new BsonDocument
            {
                { "Id", ObjectId.GenerateNewId() },
                { "Username", "ada" },
                { "ExtraField", "this would normally throw" }
            }.ToBson();

            // Should NOT throw — [BsonIgnoreExtraElements] is on UserProfile.
            var roundTripped = BsonSerializer.Deserialize<UserProfile>(wire);
            roundTripped.Username.Should().Be("ada");
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<UserProfile>();
            var reflection = BuildReflectionSerializer<UserProfile>();

            SerializeUsing(generated, original).Should()
                .BeEquivalentTo(SerializeUsing(reflection, original));
        }

        private static UserProfile NewSample() => new UserProfile
        {
            Id = ObjectId.GenerateNewId(),
            Username = "ada",
            InternalToken = null
        };
    }
}
