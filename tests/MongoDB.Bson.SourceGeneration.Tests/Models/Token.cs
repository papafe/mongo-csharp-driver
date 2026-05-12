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
    // Exercises batch 5 of ticket #3: [BsonSerializer(typeof(X))] passthrough. The generator emits
    // `new UpperCaseStringSerializer()` and routes Code's read/write through it instead of the
    // inline string primitive path.
    public class Token
    {
        public ObjectId Id { get; set; }

        [BsonSerializer(typeof(UpperCaseStringSerializer))]
        public string Code { get; set; }
    }
}
