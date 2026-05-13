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
    // [BsonIgnore] drops a member from the output; [BsonIgnoreExtraElements] permits unknown
    // elements to flow through deserialize without throwing.
    public class MemberIgnoreTests
    {
        static MemberIgnoreTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void BsonIgnore_Drops_Member_From_Output()
        {
            var profile = new UserProfile
            {
                Id = ObjectId.GenerateNewId(),
                Username = "ada",
                InternalToken = "secret"
            };
            var doc = BsonDocument.Parse(profile.ToJson());

            doc.Contains("InternalToken").Should().BeFalse();
        }

        [Fact]
        public void BsonIgnoreExtraElements_Allows_Unknown_Elements_On_Deserialize()
        {
            var wire = new BsonDocument
            {
                { "Id", ObjectId.GenerateNewId() },
                { "Username", "ada" },
                { "ExtraField", "this would normally throw" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<UserProfile>(wire);
            result.Username.Should().Be("ada");
        }
    }
}
