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
    // Enum support landed 2026-05-15. Before that, enum members fell through to LookupSerializer
    // (functional but not AOT-clean) and `[BsonRepresentation]` on an enum was silently dropped.
    // The current emit routes through a cached `EnumSerializer<TEnum>` instance per member,
    // optionally constructed with the BsonType representation argument from [BsonRepresentation].
    public class EnumTests
    {
        static EnumTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Plain_Enum_Member_Is_Byte_Identical_To_Reflection()
        {
            var sample = new UserAccount
            {
                Id = ObjectId.GenerateNewId(),
                State = AccountState.Active,
                AccessRights = AccessRights.Read | AccessRights.Write
            };
            AssertByteIdenticalToReflection(sample);
        }

        [Fact]
        public void Plain_State_Default_Representation_Tracks_Underlying_Int32()
        {
            // EnumSerializer<TEnum>'s parameterless ctor picks the wire representation from
            // Type.GetTypeCode(GetUnderlyingType(TEnum)) — Int32 for AccountState, Int64 for AccessRights.
            var v = new UserAccount
            {
                Id = ObjectId.GenerateNewId(),
                State = AccountState.Active,
                AccessRights = AccessRights.None
            };
            var doc = BsonSerializer.Deserialize<BsonDocument>(v.ToBson());

            doc["State"].BsonType.Should().Be(BsonType.Int32);
            doc["State"].AsInt32.Should().Be((int)AccountState.Active);
        }

        [Fact]
        public void Plain_AccessRights_Default_Representation_Tracks_Underlying_Int64()
        {
            var v = new UserAccount
            {
                Id = ObjectId.GenerateNewId(),
                State = AccountState.Inactive,
                AccessRights = AccessRights.Read | AccessRights.Write
            };
            var doc = BsonSerializer.Deserialize<BsonDocument>(v.ToBson());

            doc["AccessRights"].BsonType.Should().Be(BsonType.Int64);
            doc["AccessRights"].AsInt64.Should().Be((long)(AccessRights.Read | AccessRights.Write));
        }

        [Fact]
        public void BsonRepresentation_String_Encodes_Enum_As_BsonString()
        {
            var v = new UserAccountAsString
            {
                Id = ObjectId.GenerateNewId(),
                State = AccountState.Suspended,
                AccessRights = AccessRights.Admin
            };
            var doc = BsonSerializer.Deserialize<BsonDocument>(v.ToBson());

            doc["State"].BsonType.Should().Be(BsonType.String);
            doc["State"].AsString.Should().Be("Suspended");

            doc["AccessRights"].BsonType.Should().Be(BsonType.String);
            doc["AccessRights"].AsString.Should().Be("Admin");
        }

        [Fact]
        public void BsonRepresentation_String_Encodes_Flags_Combination_As_BsonString()
        {
            // [Flags] enum with multiple bits set goes out as "Read, Write" (the runtime
            // EnumSerializer follows Enum.ToString conventions for the string representation).
            var v = new UserAccountAsString
            {
                Id = ObjectId.GenerateNewId(),
                State = AccountState.Inactive,
                AccessRights = AccessRights.Read | AccessRights.Write
            };
            var doc = BsonSerializer.Deserialize<BsonDocument>(v.ToBson());

            doc["AccessRights"].AsString.Should().Be("Read, Write");
        }

        [Fact]
        public void BsonRepresentation_String_Output_Is_Byte_Identical_To_Reflection()
        {
            var sample = new UserAccountAsString
            {
                Id = ObjectId.GenerateNewId(),
                State = AccountState.Active,
                AccessRights = AccessRights.Read | AccessRights.Write
            };
            AssertByteIdenticalToReflection(sample);
        }

        [Fact]
        public void Round_Trip_Preserves_Enum_Values_For_Both_Shapes()
        {
            AssertFourWayCross(
                new UserAccount
                {
                    Id = ObjectId.GenerateNewId(),
                    State = AccountState.Active,
                    AccessRights = AccessRights.Read | AccessRights.Write
                },
                (label, v) =>
                {
                    v.State.Should().Be(AccountState.Active, label);
                    v.AccessRights.Should().Be(AccessRights.Read | AccessRights.Write, label);
                });

            AssertFourWayCross(
                new UserAccountAsString
                {
                    Id = ObjectId.GenerateNewId(),
                    State = AccountState.Suspended,
                    AccessRights = AccessRights.Admin
                },
                (label, v) =>
                {
                    v.State.Should().Be(AccountState.Suspended, label);
                    v.AccessRights.Should().Be(AccessRights.Admin, label);
                });
        }
    }
}
