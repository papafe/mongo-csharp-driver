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
using MongoDB.Bson.Serialization.Serializers;

namespace MongoDB.Bson.SourceGeneration.Benchmarks
{
    // Builds the two flavours of serializer we compare in every benchmark:
    //   - Reflection: a `BsonClassMapSerializer<T>` built directly from a fresh class map. We
    //     don't go through `BsonSerializer.LookupSerializer<T>` because the registry is global
    //     and once the BenchmarkContext is registered every lookup for the listed types returns
    //     the source-gen instance.
    //   - Source-gen: pulled from the context's provider. The provider is materialised lazily by
    //     the base class; touching `.Provider` triggers `CreateProvider` and the per-type cached
    //     fields inside `GeneratedProvider`.
    //
    // Both serializers are warm at this point — no first-use cost in the benchmark body.
    internal static class SerializerFactory
    {
        public static IBsonSerializer<T> BuildReflection<T>()
        {
            var classMap = new BsonClassMap<T>(cm => cm.AutoMap());
            classMap.Freeze();
            return new BsonClassMapSerializer<T>(classMap);
        }

        public static IBsonSerializer<T> BuildSourceGen<T>()
        {
            // Touching .Provider on the singleton instantiates the GeneratedProvider exactly once
            // for the process. Subsequent calls (across benchmark classes) get the same instance.
            var provider = BenchmarkContext.Default.Provider;
            return (IBsonSerializer<T>)provider.GetSerializer(typeof(T))!;
        }
    }
}
