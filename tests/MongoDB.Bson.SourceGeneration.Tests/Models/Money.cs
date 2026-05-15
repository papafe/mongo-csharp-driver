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

using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Bare decimal/float — go out through the cached DecimalSerializer / SingleSerializer
    // instances. Default representation: decimal → BsonType.Decimal128, float → BsonType.Double.
    public class MoneyEntry
    {
        public ObjectId Id { get; set; }
        public decimal Amount { get; set; }
        public float Ratio { get; set; }
    }

    // Same two CLR types with [BsonRepresentation] overrides exercising the
    // per-member-cached-serializer path. Until the decimal/float promotion landed, both attributes
    // were silently no-ops; this POCO is what proves the round-trip respects the override.
    public class MoneyEntryAsString
    {
        public ObjectId Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public decimal Amount { get; set; }

        [BsonRepresentation(BsonType.String)]
        public float Ratio { get; set; }
    }
}
