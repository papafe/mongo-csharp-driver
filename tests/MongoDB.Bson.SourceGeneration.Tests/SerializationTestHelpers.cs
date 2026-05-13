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
using System.IO;
using FluentAssertions;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Helpers shared by every parity-style test. The reflection serializer is constructed directly
    // from a frozen BsonClassMap so that it bypasses BsonSerializer's registry — which the
    // generated context intercepts for any type listed in TestContext.
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

        // Asserts that the generator's output for `sample` is byte-identical to what
        // BsonClassMapSerializer<T> produces — the strong contract on the wire layer.
        public static void AssertByteIdenticalToReflection<T>(T sample)
        {
            var generated = BsonSerializer.LookupSerializer<T>();
            var reflection = BuildReflectionSerializer<T>();
            SerializeUsing(generated, sample).Should()
                .BeEquivalentTo(SerializeUsing(reflection, sample));
        }

        // Runs every (serializer × deserializer) combination over `sample` and applies the caller's
        // assertion to each result. Lets a single test prove the generator and reflection paths
        // round-trip equal in-memory values, including cross-deserialization between the two paths.
        public static void AssertFourWayCross<T>(T sample, Action<string, T> assert)
        {
            var generated = BsonSerializer.LookupSerializer<T>();
            var reflection = BuildReflectionSerializer<T>();
            var bytesGen = SerializeUsing(generated, sample);
            var bytesRefl = SerializeUsing(reflection, sample);

            assert("gen->gen", DeserializeUsing(generated, bytesGen));
            assert("gen->refl", DeserializeUsing(reflection, bytesGen));
            assert("refl->gen", DeserializeUsing(generated, bytesRefl));
            assert("refl->refl", DeserializeUsing(reflection, bytesRefl));
        }
    }
}
