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

using System;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    public enum AccountState
    {
        Inactive = 0,
        Active = 1,
        Suspended = 2
    }

    [Flags]
    public enum AccessRights : long
    {
        None = 0,
        Read = 1,
        Write = 2,
        Admin = 4
    }

    // Plain enum members — go out through cached `EnumSerializer<AccountState>` /
    // `EnumSerializer<AccessRights>` instances. Default wire representation tracks the enum's
    // underlying type (Int32 for AccountState, Int64 for AccessRights).
    public class UserAccount
    {
        public ObjectId Id { get; set; }
        public AccountState State { get; set; }
        public AccessRights AccessRights { get; set; }
    }

    // [BsonRepresentation(BsonType.String)] re-encodes the enum value as a BSON string
    // ("Active", "Read, Write"). Validates that the per-member representation override flows
    // through to the cached EnumSerializer's ctor.
    public class UserAccountAsString
    {
        public ObjectId Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public AccountState State { get; set; }

        [BsonRepresentation(BsonType.String)]
        public AccessRights AccessRights { get; set; }
    }
}
