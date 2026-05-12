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
    // Exercises explicit [BsonId] and [BsonElement] attribute handling on Order:
    // - [BsonId] on a non-"Id" member (OrderKey) must map to _id.
    // - [BsonElement("customer_name")] must override the default wire name for CustomerName.
    public class OrderTests
    {
        static OrderTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void BsonId_Maps_NonId_Member_To_Underscore_Id()
        {
            var order = NewSample();
            var doc = BsonDocument.Parse(order.ToJson());

            doc.Contains("_id").Should().BeTrue();
            doc.Contains("OrderKey").Should().BeFalse();
            doc["_id"].AsObjectId.Should().Be(order.OrderKey);
        }

        [Fact]
        public void BsonElement_Overrides_Wire_Name()
        {
            var order = NewSample();
            var doc = BsonDocument.Parse(order.ToJson());

            doc.Contains("customer_name").Should().BeTrue();
            doc.Contains("CustomerName").Should().BeFalse();
            doc["customer_name"].AsString.Should().Be(order.CustomerName);
        }

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Order>();
            var reflection = BuildReflectionSerializer<Order>();

            SerializeUsing(generated, original).Should()
                .BeEquivalentTo(SerializeUsing(reflection, original));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = NewSample();
            var generated = BsonSerializer.LookupSerializer<Order>();
            var reflection = BuildReflectionSerializer<Order>();

            var bytesGen = SerializeUsing(generated, original);
            var bytesRefl = SerializeUsing(reflection, original);

            foreach (var (label, value) in new[]
                     {
                         ("gen→gen", DeserializeUsing(generated, bytesGen)),
                         ("gen→refl", DeserializeUsing(reflection, bytesGen)),
                         ("refl→gen", DeserializeUsing(generated, bytesRefl)),
                         ("refl→refl", DeserializeUsing(reflection, bytesRefl)),
                     })
            {
                value.OrderKey.Should().Be(original.OrderKey, label);
                value.CustomerName.Should().Be(original.CustomerName, label);
                value.Quantity.Should().Be(original.Quantity, label);
            }
        }

        private static Order NewSample() => new Order
        {
            OrderKey = ObjectId.GenerateNewId(),
            CustomerName = "Ada Lovelace",
            Quantity = 3
        };
    }
}
