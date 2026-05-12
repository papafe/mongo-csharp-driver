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
    // Baseline POCO: all properties, no attributes. Exercises the default Id-by-name
    // convention (Id → _id) and primitive read/write for ObjectId / string / int.
    public class SimplePerson
    {
        public ObjectId Id { get; set; }

        public string Name { get; set; }

        public int Age { get; set; }
    }
}
