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
    // Nests an Address. Both Contact and Address are listed in TestContext so the
    // generated ContactSerializer's BsonSerializer.LookupSerializer<Address>() fallback
    // resolves to the generated AddressSerializer at runtime.
    public class Contact
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public Address HomeAddress { get; set; }
    }
}
