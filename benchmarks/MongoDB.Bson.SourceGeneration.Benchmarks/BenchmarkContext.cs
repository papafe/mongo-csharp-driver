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
using MongoDB.Bson.SourceGeneration.Benchmarks.Models;

namespace MongoDB.Bson.SourceGeneration.Benchmarks
{
    // Lists every POCO the benchmarks compare. The generator emits one serializer class per
    // type and routes lookups for these types through the source-gen path after `Register()`.
    [BsonSerializable(typeof(SimplePoco))]
    [BsonSerializable(typeof(NestedPoco))]
    [BsonSerializable(typeof(Address))]
    [BsonSerializable(typeof(AttributedPoco))]
    public partial class BenchmarkContext : BsonSerializerContext
    {
    }
}
