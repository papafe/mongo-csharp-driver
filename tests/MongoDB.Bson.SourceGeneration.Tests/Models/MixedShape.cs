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

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Interleaves fields and properties in source so wire-order parity is observable:
    // the runtime emits all fields first, then all properties (in source order within each
    // group). The generator must do the same.
    public class MixedShape
    {
        public string FirstProperty { get; set; }
        public int FirstField;
        public string SecondProperty { get; set; }
        public int SecondField;
    }
}
