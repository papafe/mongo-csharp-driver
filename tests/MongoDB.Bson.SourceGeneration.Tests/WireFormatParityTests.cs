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

using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using static MongoDB.Bson.SourceGeneration.Tests.SerializationTestHelpers;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // The strong contract: for each POCO in TestContext, the generated serializer's output is
    // byte-identical to BsonClassMapSerializer<T>, and the four-way cross-deserialization matrix
    // (gen->gen, gen->refl, refl->gen, refl->refl) preserves in-memory values. One section per
    // POCO; helpers in SerializationTestHelpers do the heavy lifting.
    public class WireFormatParityTests
    {
        static WireFormatParityTests()
        {
            TestContext.Default.Register();
        }

        // --- SimplePerson ---

        [Fact]
        public void SimplePerson_ByteIdentical()
            => AssertByteIdenticalToReflection(NewSimplePerson());

        [Fact]
        public void SimplePerson_FourWayCross()
            => AssertFourWayCross(NewSimplePerson(), (label, v) =>
            {
                v.Name.Should().Be("Ada Lovelace", label);
                v.Age.Should().Be(36, label);
            });

        // --- Order ---

        [Fact]
        public void Order_ByteIdentical()
            => AssertByteIdenticalToReflection(NewOrder());

        [Fact]
        public void Order_FourWayCross()
            => AssertFourWayCross(NewOrder(), (label, v) =>
            {
                v.CustomerName.Should().Be("Ada Lovelace", label);
                v.Quantity.Should().Be(3, label);
            });

        // --- Coordinates ---

        [Fact]
        public void Coordinates_ByteIdentical()
            => AssertByteIdenticalToReflection(NewCoordinates());

        [Fact]
        public void Coordinates_FourWayCross()
            => AssertFourWayCross(NewCoordinates(), (label, v) =>
            {
                v.Latitude.Should().Be(51.4934, label);
                v.Longitude.Should().Be(0.0098, label);
                v.Label.Should().Be("Greenwich", label);
            });

        // --- UserProfile ---

        [Fact]
        public void UserProfile_ByteIdentical()
            => AssertByteIdenticalToReflection(NewUserProfile());

        // --- Customer ---

        [Fact]
        public void Customer_ByteIdentical_With_Null_Email()
            => AssertByteIdenticalToReflection(new Customer { Id = ObjectId.GenerateNewId(), Email = null, LoyaltyPoints = 7 });

        // --- Inventory ---

        [Fact]
        public void Inventory_ByteIdentical()
            => AssertByteIdenticalToReflection(NewInventory());

        [Fact]
        public void Inventory_FourWayCross()
            => AssertFourWayCross(NewInventory(), (label, v) =>
            {
                v.Count.Should().Be(12, label);
                v.Extras["color"].AsString.Should().Be("red", label);
                v.Extras["weight"].AsDouble.Should().Be(1.5, label);
            });

        // --- TaggedRecord ---

        [Fact]
        public void TaggedRecord_ByteIdentical()
            => AssertByteIdenticalToReflection(NewTaggedRecord());

        [Fact]
        public void TaggedRecord_FourWayCross()
            => AssertFourWayCross(NewTaggedRecord(), (label, v) =>
            {
                v.Owner.Should().Be("Grace", label);
                v.Extras["tag"].Should().Be("beta", label);
                v.Extras["priority"].Should().Be(9, label);
            });

        // --- MixedShape ---

        [Fact]
        public void MixedShape_ByteIdentical()
            => AssertByteIdenticalToReflection(NewMixedShape());

        // --- Contact ---

        [Fact]
        public void Contact_ByteIdentical()
            => AssertByteIdenticalToReflection(NewContact());

        [Fact]
        public void Contact_FourWayCross()
            => AssertFourWayCross(NewContact(), (label, v) =>
            {
                v.Name.Should().Be("Ada Lovelace", label);
                v.HomeAddress.Street.Should().Be("5 Wheatstone Way", label);
                v.HomeAddress.City.Should().Be("London", label);
            });

        // --- Measurement ---

        [Fact]
        public void Measurement_ByteIdentical()
            => AssertByteIdenticalToReflection(NewMeasurement());

        [Fact]
        public void Measurement_FourWayCross()
            => AssertFourWayCross(NewMeasurement(), (label, v) =>
            {
                v.Count.Should().Be(42, label);
                v.Magnitude.Should().Be(1000, label);
            });

        // --- Token ---

        [Fact]
        public void Token_ByteIdentical()
            => AssertByteIdenticalToReflection(NewToken());

        [Fact]
        public void Token_FourWayCross()
            => AssertFourWayCross(NewToken(), (label, v) =>
                v.Code.Should().Be("ABC-123", label));

        // --- Animal / Cat / Dog (polymorphism via nominal-type Animal) ---

        [Fact]
        public void Animal_AsItself_ByteIdentical()
            => AssertByteIdenticalToReflection(new Animal { Id = ObjectId.GenerateNewId(), Name = "Generic" });

        [Fact]
        public void Animal_AsCat_ByteIdentical()
            => AssertByteIdenticalToReflection<Animal>(new Cat { Id = ObjectId.GenerateNewId(), Name = "Whiskers", LikesYarn = true });

        [Fact]
        public void Animal_AsDog_ByteIdentical()
            => AssertByteIdenticalToReflection<Animal>(new Dog { Id = ObjectId.GenerateNewId(), Name = "Rex", Breed = "Labrador" });

        [Fact]
        public void Animal_AsCat_FourWayCross()
        {
            Animal original = new Cat { Id = ObjectId.GenerateNewId(), Name = "Whiskers", LikesYarn = true };
            AssertFourWayCross(original, (label, v) =>
            {
                v.Should().BeOfType<Cat>(label);
                v.Name.Should().Be("Whiskers", label);
                ((Cat)v).LikesYarn.Should().BeTrue(label);
            });
        }

        // --- PersonRecord (positional record) ---

        [Fact]
        public void PersonRecord_ByteIdentical()
            => AssertByteIdenticalToReflection(new PersonRecord("Ada", 36));

        [Fact]
        public void PersonRecord_FourWayCross()
            => AssertFourWayCross(new PersonRecord("Ada", 36), (label, v) =>
            {
                v.Name.Should().Be("Ada", label);
                v.Age.Should().Be(36, label);
            });

        // --- AccountProfile (init-only) ---

        [Fact]
        public void AccountProfile_ByteIdentical()
            => AssertByteIdenticalToReflection(NewAccountProfile());

        [Fact]
        public void AccountProfile_FourWayCross()
            => AssertFourWayCross(NewAccountProfile(), (label, v) =>
            {
                v.DisplayName.Should().Be("Ada", label);
                v.Bio.Should().Be("Mathematician", label);
            });

        // --- AppSettings (required) ---

        [Fact]
        public void AppSettings_ByteIdentical()
            => AssertByteIdenticalToReflection(NewAppSettings());

        [Fact]
        public void AppSettings_FourWayCross()
            => AssertFourWayCross(NewAppSettings(), (label, v) =>
            {
                v.Theme.Should().Be("dark", label);
                v.FontSize.Should().Be(14, label);
            });

        // --- Sample factories ---

        private static SimplePerson NewSimplePerson() => new SimplePerson
        {
            Id = ObjectId.GenerateNewId(),
            Name = "Ada Lovelace",
            Age = 36
        };

        private static Order NewOrder() => new Order
        {
            OrderKey = ObjectId.GenerateNewId(),
            CustomerName = "Ada Lovelace",
            Quantity = 3
        };

        private static Coordinates NewCoordinates() => new Coordinates
        {
            Latitude = 51.4934,
            Longitude = 0.0098,
            Label = "Greenwich"
        };

        private static UserProfile NewUserProfile() => new UserProfile
        {
            Id = ObjectId.GenerateNewId(),
            Username = "ada",
            InternalToken = null
        };

        private static Inventory NewInventory() => new Inventory
        {
            Id = ObjectId.GenerateNewId(),
            Count = 12,
            Extras = new BsonDocument { { "color", "red" }, { "weight", 1.5 } }
        };

        private static TaggedRecord NewTaggedRecord() => new TaggedRecord
        {
            Id = ObjectId.GenerateNewId(),
            Owner = "Grace",
            Extras = new Dictionary<string, object> { { "tag", "beta" }, { "priority", 9 } }
        };

        private static MixedShape NewMixedShape() => new MixedShape
        {
            FirstField = 1,
            SecondField = 2,
            FirstProperty = "one",
            SecondProperty = "two"
        };

        private static Contact NewContact() => new Contact
        {
            Id = ObjectId.GenerateNewId(),
            Name = "Ada Lovelace",
            HomeAddress = new Address { Street = "5 Wheatstone Way", City = "London" }
        };

        private static Measurement NewMeasurement() => new Measurement
        {
            Id = ObjectId.GenerateNewId(),
            Count = 42,
            Magnitude = 1000
        };

        private static Token NewToken() => new Token
        {
            Id = ObjectId.GenerateNewId(),
            Code = "abc-123"
        };

        private static AccountProfile NewAccountProfile() => new AccountProfile
        {
            Id = ObjectId.GenerateNewId(),
            DisplayName = "Ada",
            Bio = "Mathematician"
        };

        private static AppSettings NewAppSettings() => new AppSettings
        {
            Id = ObjectId.GenerateNewId(),
            Theme = "dark",
            FontSize = 14
        };
    }
}
