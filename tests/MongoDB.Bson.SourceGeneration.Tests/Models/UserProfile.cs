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
    // Exercises batch 1 of ticket #3 attributes:
    //  - [BsonNoId] on the type suppresses the Id-by-name convention (Id stays "Id" in BSON).
    //  - [BsonIgnoreExtraElements] on the type makes the deserializer skip unknowns instead of throwing.
    //  - [BsonIgnore] on a member drops it from the emitted serializer entirely.
    [BsonIgnoreExtraElements]
    [BsonNoId]
    public class UserProfile
    {
        public ObjectId Id { get; set; }

        public string Username { get; set; }

        [BsonIgnore]
        public string InternalToken { get; set; }
    }
}
