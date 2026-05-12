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
    // Exercises batch 3 of ticket #3:
    //  - [BsonRequired] throws on deserialize if missing in the BSON.
    //  - [BsonDefaultValue] assigns the default at deserialize if missing.
    //  - Both rely on the "members seen" tracking emitted in DeserializeCore.
    public class Subscription
    {
        public ObjectId Id { get; set; }

        [BsonRequired]
        public string Plan { get; set; }

        [BsonDefaultValue("monthly")]
        public string BillingPeriod { get; set; }

        [BsonDefaultValue(0)]
        public int MaxUsers { get; set; }
    }
}
