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
    // Plain (non-polymorphic) inheritance: Car inherits Brand and Year from Vehicle. The
    // extractor walks base types regardless of discriminator attributes, so CarSerializer
    // must emit all three members in base-first wire order to match BsonClassMapSerializer.
    public class Vehicle
    {
        public string Brand { get; set; }
        public int Year { get; set; }
    }

    public class Car : Vehicle
    {
        public int Doors { get; set; }
    }
}
