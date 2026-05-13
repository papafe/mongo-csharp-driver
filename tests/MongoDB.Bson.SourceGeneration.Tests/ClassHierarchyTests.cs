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
using static MongoDB.Bson.SourceGeneration.Tests.SerializationTestHelpers;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Inheritance *without* [BsonDiscriminator] / [BsonKnownTypes]. The extractor walks every
    // base type and folds inherited members into the derived serializer, so each concrete type
    // round-trips correctly when used as its own static type. Polymorphic dispatch via a base
    // nominal type is intentionally not exercised here — that's the discriminator case covered
    // by PolymorphismTests.
    public class ClassHierarchyTests
    {
        static ClassHierarchyTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Single_Level_Inheritance_Round_Trips()
        {
            var car = new Car { Brand = "Volvo", Year = 1968, Doors = 4 };
            var bytes = car.ToBson();
            var result = BsonSerializer.Deserialize<Car>(bytes);

            result.Brand.Should().Be("Volvo");
            result.Year.Should().Be(1968);
            result.Doors.Should().Be(4);
        }

        [Fact]
        public void Single_Level_Inheritance_Matches_Reflection_Wire_Format()
            => AssertByteIdenticalToReflection(new Car { Brand = "Volvo", Year = 1968, Doors = 4 });

        [Fact]
        public void Single_Level_Inheritance_Base_Type_Round_Trips_Independently()
        {
            // Vehicle itself, used as its own static type, emits only its own members.
            var v = new Vehicle { Brand = "Ford", Year = 1970 };
            var bytes = v.ToBson();
            var result = BsonSerializer.Deserialize<Vehicle>(bytes);

            result.Brand.Should().Be("Ford");
            result.Year.Should().Be(1970);
        }

        [Fact]
        public void Three_Level_Inheritance_Round_Trips()
        {
            var article = new Article
            {
                Id = ObjectId.GenerateNewId(),
                Title = "On Computable Numbers",
                Body = "..."
            };
            var bytes = article.ToBson();
            var result = BsonSerializer.Deserialize<Article>(bytes);

            result.Id.Should().Be(article.Id);
            result.Title.Should().Be(article.Title);
            result.Body.Should().Be(article.Body);
        }

        [Fact]
        public void Three_Level_Inheritance_Matches_Reflection_Wire_Format()
            => AssertByteIdenticalToReflection(new Article
            {
                Id = ObjectId.GenerateNewId(),
                Title = "On Computable Numbers",
                Body = "An exposition."
            });

        [Fact]
        public void Three_Level_Inheritance_Wire_Order_Is_Base_To_Derived()
        {
            var article = new Article
            {
                Id = ObjectId.GenerateNewId(),
                Title = "T",
                Body = "B"
            };
            var doc = BsonDocument.Parse(article.ToJson());

            // BsonClassMap.AllMemberMaps order: root level first, then each derived level.
            // For Article: _id (Entity), Title (Document), Body (Article).
            doc.ElementCount.Should().Be(3);
            doc.GetElement(0).Name.Should().Be("_id");
            doc.GetElement(1).Name.Should().Be("Title");
            doc.GetElement(2).Name.Should().Be("Body");
        }

        [Fact]
        public void Three_Level_Inheritance_Four_Way_Cross()
            => AssertFourWayCross(
                new Article { Id = ObjectId.GenerateNewId(), Title = "T", Body = "B" },
                (label, v) =>
                {
                    v.Title.Should().Be("T", label);
                    v.Body.Should().Be("B", label);
                });

        [Fact]
        public void Record_Inheriting_Record_Round_Trips()
        {
            var f = new Flower("Rosa", "Red");
            var bytes = f.ToBson();
            var result = BsonSerializer.Deserialize<Flower>(bytes);

            result.Species.Should().Be("Rosa");
            result.Color.Should().Be("Red");
        }

        [Fact]
        public void Record_Inheriting_Record_Matches_Reflection_Wire_Format()
            => AssertByteIdenticalToReflection(new Flower("Rosa", "Red"));

        [Fact]
        public void Record_Inheriting_Record_Four_Way_Cross()
            => AssertFourWayCross(new Flower("Rosa", "Red"), (label, v) =>
            {
                v.Species.Should().Be("Rosa", label);
                v.Color.Should().Be("Red", label);
            });
    }
}
