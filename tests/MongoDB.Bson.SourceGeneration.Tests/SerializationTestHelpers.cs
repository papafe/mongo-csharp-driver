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

using System.IO;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Helpers shared by every per-POCO parity test. The reflection serializer is
    // constructed directly from a frozen BsonClassMap so that it bypasses
    // BsonSerializer's registry — which the generated context intercepts for any
    // type listed in TestContext.
    internal static class SerializationTestHelpers
    {
        public static IBsonSerializer<T> BuildReflectionSerializer<T>()
        {
            var classMap = new BsonClassMap<T>(cm => cm.AutoMap());
            classMap.Freeze();
            return new BsonClassMapSerializer<T>(classMap);
        }

        public static byte[] SerializeUsing<T>(IBsonSerializer<T> serializer, T value)
        {
            using var stream = new MemoryStream();
            using (var writer = new BsonBinaryWriter(stream))
            {
                var context = BsonSerializationContext.CreateRoot(writer);
                var args = new BsonSerializationArgs { NominalType = typeof(T) };
                serializer.Serialize(context, args, value);
            }
            return stream.ToArray();
        }

        public static T DeserializeUsing<T>(IBsonSerializer<T> serializer, byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BsonBinaryReader(stream);
            var context = BsonDeserializationContext.CreateRoot(reader);
            var args = new BsonDeserializationArgs { NominalType = typeof(T) };
            return serializer.Deserialize(context, args);
        }
    }
}
