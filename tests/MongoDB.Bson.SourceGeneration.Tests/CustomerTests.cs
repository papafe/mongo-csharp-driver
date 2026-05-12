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
    public class CustomerTests
    {
        static CustomerTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void IgnoreIfNull_Omits_Null_Email()
        {
            var c = new Customer { Id = ObjectId.GenerateNewId(), Email = null, LoyaltyPoints = 50 };
            var doc = BsonDocument.Parse(c.ToJson());

            doc.Contains("Email").Should().BeFalse();
            doc.Contains("LoyaltyPoints").Should().BeTrue();
        }

        [Fact]
        public void IgnoreIfNull_Writes_NonNull_Email()
        {
            var c = new Customer { Id = ObjectId.GenerateNewId(), Email = "ada@example.com", LoyaltyPoints = 50 };
            var doc = BsonDocument.Parse(c.ToJson());

            doc.Contains("Email").Should().BeTrue();
            doc["Email"].AsString.Should().Be("ada@example.com");
        }

        [Fact]
        public void IgnoreIfDefault_Omits_Zero_LoyaltyPoints()
        {
            var c = new Customer { Id = ObjectId.GenerateNewId(), Email = "ada@example.com", LoyaltyPoints = 0 };
            var doc = BsonDocument.Parse(c.ToJson());

            doc.Contains("LoyaltyPoints").Should().BeFalse();
            doc.Contains("Email").Should().BeTrue();
        }

        [Fact]
        public void IgnoreIfDefault_Writes_NonZero_LoyaltyPoints()
        {
            var c = new Customer { Id = ObjectId.GenerateNewId(), Email = "ada@example.com", LoyaltyPoints = 42 };
            var doc = BsonDocument.Parse(c.ToJson());

            doc.Contains("LoyaltyPoints").Should().BeTrue();
            doc["LoyaltyPoints"].AsInt32.Should().Be(42);
        }

        [Fact]
        public void Wire_Format_Matches_Reflection_With_Mixed_Values()
        {
            // Combine null email + non-zero points to exercise both conditions in one wire.
            var c = new Customer { Id = ObjectId.GenerateNewId(), Email = null, LoyaltyPoints = 7 };
            var generated = BsonSerializer.LookupSerializer<Customer>();
            var reflection = BuildReflectionSerializer<Customer>();

            SerializeUsing(generated, c).Should()
                .BeEquivalentTo(SerializeUsing(reflection, c));
        }
    }
}
