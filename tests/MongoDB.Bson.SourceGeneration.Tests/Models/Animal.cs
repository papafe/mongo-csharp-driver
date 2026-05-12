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
    // Root of a polymorphic hierarchy. RootClass=true forces _t to be written on every actual
    // type in the chain (Animal, Cat, Dog). [BsonKnownTypes] tells the generator which subtype
    // serializers to dispatch into.
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(Cat), typeof(Dog))]
    public class Animal
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
    }

    public class Cat : Animal
    {
        public bool LikesYarn { get; set; }
    }

    public class Dog : Animal
    {
        public string Breed { get; set; }
    }
}
