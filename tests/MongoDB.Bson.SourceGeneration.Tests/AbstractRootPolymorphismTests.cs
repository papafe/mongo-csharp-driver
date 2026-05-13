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
    // Abstract polymorphic root (Account) with two concrete subtypes (SavingsAccount,
    // CheckingAccount). The generator emits AccountSerializer with discriminator dispatch only —
    // no DeserializeCore, no member-writing tail. Subtypes get the normal full shape including
    // members inherited from Account.
    public class AbstractRootPolymorphismTests
    {
        static AbstractRootPolymorphismTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Subtype_Round_Trips_Through_Abstract_Base()
        {
            Account asAccount = new SavingsAccount
            {
                Id = ObjectId.GenerateNewId(),
                Balance = 1234.56m,
                InterestRate = 0.04
            };

            var bytes = asAccount.ToBson<Account>();
            var result = BsonSerializer.Deserialize<Account>(bytes);

            result.Should().BeOfType<SavingsAccount>();
            ((SavingsAccount)result).InterestRate.Should().Be(0.04);
            result.Balance.Should().Be(1234.56m);
        }

        [Fact]
        public void Serialize_Through_Abstract_Base_Writes_Subtype_Discriminator()
        {
            Account asAccount = new CheckingAccount
            {
                Id = ObjectId.GenerateNewId(),
                Balance = 50m,
                OverdraftLimit = 200
            };
            var doc = BsonDocument.Parse(asAccount.ToJson<Account>());

            doc["_t"].AsString.Should().Be("CheckingAccount");
            doc["OverdraftLimit"].AsInt32.Should().Be(200);
        }

        [Fact]
        public void Subtype_Used_Directly_Round_Trips()
        {
            var s = new SavingsAccount
            {
                Id = ObjectId.GenerateNewId(),
                Balance = 99m,
                InterestRate = 0.025
            };

            var bytes = s.ToBson();
            var result = BsonSerializer.Deserialize<SavingsAccount>(bytes);

            result.Id.Should().Be(s.Id);
            result.Balance.Should().Be(99m);
            result.InterestRate.Should().Be(0.025);
        }

        [Fact]
        public void Subtype_Bytes_Through_Abstract_Are_Identical_To_Reflection()
            => AssertByteIdenticalToReflection<Account>(new SavingsAccount
            {
                Id = ObjectId.GenerateNewId(),
                Balance = 100m,
                InterestRate = 0.05
            });

        [Fact]
        public void Subtype_Four_Way_Cross_Through_Abstract()
        {
            Account original = new SavingsAccount
            {
                Id = ObjectId.GenerateNewId(),
                Balance = 100m,
                InterestRate = 0.05
            };

            AssertFourWayCross(original, (label, v) =>
            {
                v.Should().BeOfType<SavingsAccount>(label);
                v.Balance.Should().Be(100m, label);
                ((SavingsAccount)v).InterestRate.Should().Be(0.05, label);
            });
        }

        [Fact]
        public void Deserialize_With_Self_Discriminator_Throws()
        {
            var wire = new BsonDocument
            {
                { "_t", "Account" },
                { "_id", ObjectId.GenerateNewId() },
                { "Balance", 1m }
            }.ToBson();

            var ex = Assert.Throws<BsonSerializationException>(
                () => BsonSerializer.Deserialize<Account>(wire));
            ex.Message.Should().Contain("abstract").And.Contain("Account");
        }

        [Fact]
        public void Deserialize_Without_Discriminator_Throws()
        {
            // No _t on the wire — the dispatch falls into case null:, which throws for abstract.
            var wire = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "Balance", 1m }
            }.ToBson();

            Assert.Throws<BsonSerializationException>(
                () => BsonSerializer.Deserialize<Account>(wire));
        }

        [Fact]
        public void Abstract_Serializer_Has_No_DeserializeCore()
        {
            // Indirect verification: the emitted AccountSerializer shouldn't expose anything beyond
            // SerializerBase<Account>'s public surface. Reflectively spotting a private static
            // DeserializeCore would fail here even if the test method name promises otherwise.
            var serializer = BsonSerializer.LookupSerializer<Account>();
            var deserializeCore = serializer.GetType()
                .GetMethod("DeserializeCore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            deserializeCore.Should().BeNull("abstract roots shouldn't emit DeserializeCore");
        }
    }
}
