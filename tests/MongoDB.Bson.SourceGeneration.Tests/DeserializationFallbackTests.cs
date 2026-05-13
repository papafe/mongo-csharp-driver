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
    // Behavior when a member is missing from the BSON document at deserialize time.
    //   [BsonRequired]     — throws BsonSerializationException with the member name.
    //   [BsonDefaultValue] — assigns the literal default; skipped if the element is present.
    public class DeserializationFallbackTests
    {
        static DeserializationFallbackTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Required_Member_Missing_Throws_On_Deserialize()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "BillingPeriod", "annual" },
                { "MaxUsers", 10 }
            }.ToBson();

            var ex = Assert.Throws<BsonSerializationException>(
                () => BsonSerializer.Deserialize<Subscription>(wire));
            ex.Message.Should().Contain("Plan").And.Contain("was not found");
        }

        [Fact]
        public void DefaultValue_Applied_When_Member_Missing()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Plan", "free" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<Subscription>(wire);

            result.Plan.Should().Be("free");
            result.BillingPeriod.Should().Be("monthly");
            result.MaxUsers.Should().Be(0);
        }

        [Fact]
        public void DefaultValue_Skipped_When_Member_Present()
        {
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Plan", "pro" },
                { "BillingPeriod", "annual" },
                { "MaxUsers", 25 }
            }.ToBson();

            var result = BsonSerializer.Deserialize<Subscription>(wire);

            result.BillingPeriod.Should().Be("annual");
            result.MaxUsers.Should().Be(25);
        }
    }
}
