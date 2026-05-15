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

namespace MongoDB.Bson.SourceGeneration.Benchmarks.Models
{
    // Exercises the in-context nested-serializer direct-call path on the source-gen side. The
    // generator emits `s_addressSerializer.Deserialize(...)` for the Address member because
    // Address is also listed in the context — bypassing the registry. Reflection has to hit
    // BsonSerializer.LookupSerializer<Address>() on every call (cached after the first).
    public class NestedPoco
    {
        public ObjectId Id { get; set; }
        public string CustomerName { get; set; }
        public Address ShippingAddress { get; set; }
        public Address BillingAddress { get; set; }
    }

    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
    }
}
