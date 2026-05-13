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
    // A second context, separate from TestContext, that exercises the layered DefaultGuidRepresentation
    // option across all three override sites:
    //   - Context-wide BsonSourceGenerationOptions = Standard (applies to WithGuid).
    //   - Per-[BsonSerializable] override = JavaLegacy (applies to WithGuidPerListingOverride).
    //   - Per-POCO [BsonSerializationOverride] = PythonLegacy on the type itself
    //     (applies to WithGuidPerPocoOverride).
    //   - Per-member [BsonRepresentation(BsonType.String)] still wins over all of the above
    //     (applies to WithGuidExplicit.Id).
    [BsonSourceGenerationOptions(DefaultGuidRepresentation = GuidRepresentation.Standard)]
    [BsonSerializable(typeof(WithGuid))]
    [BsonSerializable(typeof(WithGuidPerListingOverride), DefaultGuidRepresentation = GuidRepresentation.JavaLegacy)]
    [BsonSerializable(typeof(WithGuidPerPocoOverride))]
    [BsonSerializable(typeof(WithGuidExplicit))]
    public partial class GuidTestContext : BsonSerializerContext
    {
    }
}
