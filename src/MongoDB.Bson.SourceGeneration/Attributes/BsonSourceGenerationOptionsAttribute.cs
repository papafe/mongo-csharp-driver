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
    /// Context-wide defaults for the BSON source generator. Applied to a partial class
    /// that derives from <see cref="BsonSerializerContext"/>. Per-type overrides are
    /// available on <see cref="BsonSerializableAttribute"/> and
    /// <see cref="BsonSerializationOverrideAttribute"/>; per-member <c>[Bson*]</c>
    /// attributes always take precedence.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BsonSourceGenerationOptionsAttribute : Attribute
    {
        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="BsonSourceGenerationOptionsAttribute"/> class.
        /// </summary>
        public BsonSourceGenerationOptionsAttribute()
        {
        }

        // public properties
        /// <summary>
        /// Default naming policy for BSON element names. Members with an explicit
        /// <c>[BsonElement("...")]</c> are unaffected.
        /// </summary>
        public BsonNamingPolicy PropertyNamingPolicy { get; set; }
    }
}
