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

namespace MongoDB.Bson.Serialization
{
    /// <summary>
    /// Base class for source-generated BSON serializer contexts. Derive a partial class from
    /// this type, decorate it with one or more
    /// <see cref="MongoDB.Bson.Serialization.Attributes.BsonSerializableAttribute"/> instances,
    /// and the source generator will emit a serializer for each listed type plus the
    /// <see cref="GetSerializer(Type)"/> override that dispatches to them.
    /// </summary>
    /// <remarks>
    /// Call <see cref="Register"/> at application startup to push this context onto the BSON
    /// serializer provider stack. Once registered, lookups for the listed types resolve to the
    /// generated serializers; types not in any context continue to flow through the existing
    /// reflection-based pipeline unchanged.
    /// </remarks>
    public abstract class BsonSerializerContext : IBsonSerializationProvider
    {
        // public methods
        /// <summary>
        /// Returns the generated serializer for <paramref name="type"/>, or <c>null</c> if this
        /// context does not handle the type. Implemented by the source generator in the
        /// derived partial class.
        /// </summary>
        /// <param name="type">The type to look up a serializer for.</param>
        /// <returns>The serializer, or <c>null</c> when not handled.</returns>
        public abstract IBsonSerializer GetSerializer(Type type);

        /// <summary>
        /// Registers this context as a BSON serialization provider. Subsequent serializer
        /// lookups for the context's listed types resolve here before any built-in provider.
        /// </summary>
        public void Register()
        {
            BsonSerializer.RegisterSerializationProvider(this);
        }
    }
}
