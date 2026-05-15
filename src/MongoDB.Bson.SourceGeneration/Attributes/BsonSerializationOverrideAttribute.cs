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

namespace MongoDB.Bson.Serialization.Attributes
{
    /// <summary>
    /// Overrides context-wide options for a single POCO, applied directly on the type. Useful when
    /// one type wants a different option from its containing context (for example, snake-case
    /// elements inside an otherwise-camelCase context) and the user owns the POCO source. For
    /// third-party POCOs the user can't modify, set the same option as a named argument on the
    /// type's <see cref="BsonSerializableAttribute"/> in the context instead.
    /// </summary>
    /// <remarks>
    /// The same option set is mirrored across <see cref="BsonSourceGenerationOptionsAttribute"/>
    /// (context-wide default), <see cref="BsonSerializableAttribute"/> (per-listing override), and
    /// this attribute (per-POCO override). Effective value for a given type is computed by left-fold:
    /// context → per-listing → per-POCO → existing per-type / per-member <c>[Bson*]</c> attributes
    /// (always win).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class BsonSerializationOverrideAttribute : Attribute
    {
        /// <summary>
        /// Overrides the context-wide <see cref="BsonSourceGenerationOptionsAttribute.DefaultGuidRepresentation"/>
        /// for this POCO. <see cref="GuidRepresentation.Unspecified"/> (the default) means "don't
        /// override; inherit whatever the layered fold produced before this attribute."
        /// </summary>
        public GuidRepresentation DefaultGuidRepresentation { get; set; } = GuidRepresentation.Unspecified;

        /// <summary>
        /// Overrides the context-wide <see cref="BsonSourceGenerationOptionsAttribute.PropertyNamingPolicy"/>
        /// for this POCO. <see cref="BsonNamingPolicy.Unspecified"/> (the default) means "don't
        /// override; inherit whatever the layered fold produced before this attribute."
        /// </summary>
        public BsonNamingPolicy PropertyNamingPolicy { get; set; } = BsonNamingPolicy.Unspecified;
    }
}
