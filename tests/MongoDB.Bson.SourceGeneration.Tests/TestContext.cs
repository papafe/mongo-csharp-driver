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

using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // The one shared context for the test project. Each new test POCO adds a
    // [BsonSerializable] line here; the generator emits one serializer per type
    // and dispatches them all through this context's GetSerializer.
    [BsonSerializable(typeof(SimplePerson))]
    [BsonSerializable(typeof(Order))]
    [BsonSerializable(typeof(Coordinates))]
    [BsonSerializable(typeof(UserProfile))]
    [BsonSerializable(typeof(Customer))]
    [BsonSerializable(typeof(Subscription))]
    [BsonSerializable(typeof(Inventory))]
    [BsonSerializable(typeof(TaggedRecord))]
    [BsonSerializable(typeof(MixedShape))]
    [BsonSerializable(typeof(Address))]
    [BsonSerializable(typeof(Contact))]
    [BsonSerializable(typeof(Measurement))]
    [BsonSerializable(typeof(Token))]
    [BsonSerializable(typeof(Animal))]
    [BsonSerializable(typeof(Cat))]
    [BsonSerializable(typeof(Dog))]
    [BsonSerializable(typeof(PersonRecord))]
    [BsonSerializable(typeof(AccountProfile))]
    [BsonSerializable(typeof(AppSettings))]
    public partial class TestContext : BsonSerializerContext
    {
    }
}
