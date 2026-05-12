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

using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Exercises batch 4 of ticket #3: [BsonExtraElements] catch-all, IDictionary<string,object> shape.
    public class TaggedRecord
    {
        public ObjectId Id { get; set; }
        public string Owner { get; set; }

        [BsonExtraElements]
        public Dictionary<string, object> Extras { get; set; }
    }
}
