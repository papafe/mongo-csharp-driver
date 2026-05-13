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
using Xunit;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Element-name resolution: [BsonId], [BsonElement], [BsonNoId].
    public class ElementNamingTests
    {
        static ElementNamingTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void BsonId_Maps_NonId_Member_To_Underscore_Id()
        {
            var order = new Order { OrderKey = ObjectId.GenerateNewId(), CustomerName = "Ada Lovelace", Quantity = 3 };
            var doc = BsonDocument.Parse(order.ToJson());

            doc.Contains("_id").Should().BeTrue();
            doc.Contains("OrderKey").Should().BeFalse();
            doc["_id"].AsObjectId.Should().Be(order.OrderKey);
        }

        [Fact]
        public void BsonElement_Overrides_Wire_Name()
        {
            var order = new Order { OrderKey = ObjectId.GenerateNewId(), CustomerName = "Ada Lovelace", Quantity = 3 };
            var doc = BsonDocument.Parse(order.ToJson());

            doc.Contains("customer_name").Should().BeTrue();
            doc.Contains("CustomerName").Should().BeFalse();
            doc["customer_name"].AsString.Should().Be(order.CustomerName);
        }

        [Fact]
        public void BsonNoId_Suppresses_Id_To_Underscore_Id_Mapping()
        {
            var profile = new UserProfile { Id = ObjectId.GenerateNewId(), Username = "ada", InternalToken = null };
            var doc = BsonDocument.Parse(profile.ToJson());

            doc.Contains("Id").Should().BeTrue("with [BsonNoId] the member stays as 'Id'");
            doc.Contains("_id").Should().BeFalse();
        }
    }
}
