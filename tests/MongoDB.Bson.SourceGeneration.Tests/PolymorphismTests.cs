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

using FluentAssertions;
using MongoDB.Bson.Serialization;
using Xunit;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // [BsonDiscriminator] + [BsonKnownTypes] for Animal/Cat/Dog. Covers _t emission on both root
    // and subtypes, subtype dispatch on serialize and deserialize, and the inheritance walk that
    // pulls base-class members into a derived serializer.
    public class PolymorphismTests
    {
        static PolymorphismTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Discriminator_Is_Written_On_Root_Class()
        {
            var a = new Animal { Id = ObjectId.GenerateNewId(), Name = "Generic" };
            var doc = BsonDocument.Parse(a.ToJson());

            doc["_t"].AsString.Should().Be("Animal");
        }

        [Fact]
        public void Discriminator_Is_Written_On_Subtype()
        {
            var c = new Cat { Id = ObjectId.GenerateNewId(), Name = "Whiskers", LikesYarn = true };
            var doc = BsonDocument.Parse(c.ToJson());

            doc["_t"].AsString.Should().Be("Cat");
        }

        [Fact]
        public void Serialize_Through_Base_Dispatches_To_Subtype_Serializer()
        {
            Animal asAnimal = new Cat { Id = ObjectId.GenerateNewId(), Name = "Whiskers", LikesYarn = true };
            var doc = BsonDocument.Parse(asAnimal.ToJson<Animal>());

            doc["_t"].AsString.Should().Be("Cat");
            doc["LikesYarn"].AsBoolean.Should().BeTrue();
        }

        [Fact]
        public void Deserialize_Through_Base_Returns_Subtype()
        {
            var wire = new BsonDocument
            {
                { "_t", "Dog" },
                { "_id", ObjectId.GenerateNewId() },
                { "Name", "Rex" },
                { "Breed", "Labrador" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<Animal>(wire);

            result.Should().BeOfType<Dog>();
            ((Dog)result).Breed.Should().Be("Labrador");
            result.Name.Should().Be("Rex");
        }

        [Fact]
        public void Deserialize_Through_Base_With_Self_Discriminator_Returns_Base()
        {
            var wire = new BsonDocument
            {
                { "_t", "Animal" },
                { "_id", ObjectId.GenerateNewId() },
                { "Name", "Generic" }
            }.ToBson();

            var result = BsonSerializer.Deserialize<Animal>(wire);

            result.GetType().Should().Be(typeof(Animal));
            result.Name.Should().Be("Generic");
        }

        [Fact]
        public void Inherited_Members_Are_Round_Tripped_On_Subtype()
        {
            // Name lives on Animal; CatSerializer needs to include it because the extractor walks
            // base types to gather inherited members.
            var cat = new Cat { Id = ObjectId.GenerateNewId(), Name = "Whiskers", LikesYarn = true };
            var bytes = cat.ToBson();
            var result = BsonSerializer.Deserialize<Cat>(bytes);

            result.Id.Should().Be(cat.Id);
            result.Name.Should().Be("Whiskers");
            result.LikesYarn.Should().BeTrue();
        }
    }
}
