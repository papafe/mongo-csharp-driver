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

namespace MongoDB.Bson.SourceGeneration.Benchmarks.Models
{
    // Exercises the per-member cached-serializer paths (`[BsonRepresentation]`, enum members)
    // plus a renamed element, ignored member, and conditional write. Approximates a realistic
    // POCO with a mix of attributes that all flow through per-member override emit in source-gen.
    public class AttributedPoco
    {
        [BsonId]
        public ObjectId Key { get; set; }

        [BsonElement("customer_name")]
        public string CustomerName { get; set; }

        [BsonRepresentation(BsonType.String)]
        public int Count { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Tier Tier { get; set; }

        [BsonIgnoreIfNull]
        public string Notes { get; set; }

        [BsonIgnore]
        public string InternalNotes { get; set; }
    }

    public enum Tier
    {
        Bronze = 0,
        Silver = 1,
        Gold = 2,
        Platinum = 3
    }
}
