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
    /// Declares context-wide options that apply to every type listed via
    /// <see cref="BsonSerializableAttribute"/> on the same context class. Options are read once at
    /// code-generation time and baked into the emitted serializers — there's no runtime cost and
    /// no runtime override path. To override an option for a single type, set the same property on
    /// the per-type <see cref="BsonSerializableAttribute"/> or use
    /// <see cref="BsonSerializationOverrideAttribute"/> directly on the POCO.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BsonSourceGenerationOptionsAttribute : Attribute
    {
        /// <summary>
        /// The default <see cref="GuidRepresentation"/> baked into the emitted serializer for every
        /// <see cref="Guid"/>-typed member of every type in this context.
        /// <see cref="GuidRepresentation.Unspecified"/> (the default) means "use the registry default
        /// at runtime" — the emit falls through to <c>BsonSerializer.LookupSerializer&lt;Guid&gt;()</c>
        /// for Guid members and global registrations win, matching the pre-source-gen behaviour.
        /// </summary>
        public GuidRepresentation DefaultGuidRepresentation { get; set; } = GuidRepresentation.Unspecified;

        /// <summary>
        /// The default <see cref="BsonNamingPolicy"/> applied at code-generation time to every
        /// member whose wire name is not pinned by an explicit <c>[BsonElement("...")]</c>,
        /// <c>[BsonId]</c>, or the default Id-detection convention. The policy runs against the CLR
        /// member name and the transformed result is emitted verbatim — no runtime cost.
        /// <see cref="BsonNamingPolicy.Unspecified"/> (the default) means "use the CLR member name
        /// as the wire name."
        /// </summary>
        public BsonNamingPolicy PropertyNamingPolicy { get; set; } = BsonNamingPolicy.Unspecified;

        /// <summary>
        /// When <c>true</c>, every type in this context silently ignores unknown elements during
        /// deserialization instead of throwing. Equivalent to placing
        /// <c>[BsonIgnoreExtraElements]</c> on every listed type. A type can opt back into the
        /// strict default with <c>[BsonIgnoreExtraElements(false)]</c>. Default <c>false</c> —
        /// unknown elements throw, matching the reflection-path default.
        /// </summary>
        public bool IgnoreExtraElements { get; set; } = false;

        /// <summary>
        /// When <c>true</c> (the default), public read/write fields are serialized alongside
        /// properties — matching the reflection-path <c>ReadWriteMemberFinderConvention</c>. Set
        /// to <c>false</c> to drop fields from every listed type and emit properties only. The
        /// per-member <c>[BsonIgnore]</c> attribute is the per-field lever; this option is the
        /// context-wide switch.
        /// </summary>
        public bool IncludeFields { get; set; } = true;
    }
}
