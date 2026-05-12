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
    /// Marks a type as a target for compile-time BSON serializer generation in a
    /// <see cref="BsonSerializerContext"/>. Apply one instance per type to be generated;
    /// the attribute is intended for use on a partial class that derives from
    /// <see cref="BsonSerializerContext"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class BsonSerializableAttribute : Attribute
    {
        // private fields
        private readonly Type _type;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="BsonSerializableAttribute"/> class.
        /// </summary>
        /// <param name="type">The type to generate a BSON serializer for.</param>
        public BsonSerializableAttribute(Type type)
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
        }

        // public properties
        /// <summary>
        /// Gets the type to generate a BSON serializer for.
        /// </summary>
        public Type Type => _type;

        /// <summary>
        /// Overrides the property naming policy from
        /// <see cref="BsonSourceGenerationOptionsAttribute.PropertyNamingPolicy"/> for this type only.
        /// </summary>
        public BsonNamingPolicy PropertyNamingPolicy { get; set; }
    }
}
