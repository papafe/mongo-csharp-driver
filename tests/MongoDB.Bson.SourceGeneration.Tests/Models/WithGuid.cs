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
    // Exercises the DefaultGuidRepresentation option in three configurations:
    //   - WithGuid: no override — should inherit the context's BsonSourceGenerationOptions value.
    //   - WithGuidPerListingOverride: the per-[BsonSerializable] named arg overrides the context.
    //   - WithGuidPerPocoOverride: [BsonSerializationOverride] on the POCO overrides everything else.
    //   - WithGuidExplicit: per-member [BsonRepresentation] wins over any context default.
    public class WithGuid
    {
        public Guid Value { get; set; }
    }

    public class WithGuidPerListingOverride
    {
        public Guid Value { get; set; }
    }

    [BsonSerializationOverride(DefaultGuidRepresentation = GuidRepresentation.PythonLegacy)]
    public class WithGuidPerPocoOverride
    {
        public Guid Value { get; set; }
    }

    public class WithGuidExplicit
    {
        [BsonRepresentation(BsonType.String)]
        public Guid Value { get; set; }
    }

    // [BsonGuidRepresentation(GuidRepresentation.X)] is the per-member binary-byte-order lever.
    // It must beat the context-default DefaultGuidRepresentation. The context defaults to
    // Standard (in GuidTestContext); this POCO opts a single Guid member into PythonLegacy.
    public class WithGuidPerMemberAttribute
    {
        [BsonGuidRepresentation(GuidRepresentation.PythonLegacy)]
        public Guid Value { get; set; }
    }
}
