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
    // POCOs that exercise the context-default IgnoreExtraElements option:
    //   - LenientInherited has no [BsonIgnoreExtraElements] — inherits the context default.
    //   - StrictOptOut sets [BsonIgnoreExtraElements(false)] to opt back into strict mode.
    //   - LenientExplicit sets [BsonIgnoreExtraElements] explicitly. Behaviour-identical to
    //     LenientInherited but proves the explicit attribute still works when the context already
    //     defaults to lenient.
    public class LenientInherited
    {
        public string Name { get; set; }
    }

    [BsonIgnoreExtraElements(false)]
    public class StrictOptOut
    {
        public string Name { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class LenientExplicit
    {
        public string Name { get; set; }
    }
}
