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

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // A separate context dedicated to PropertyNamingPolicy. The context's wide default is
    // CamelCase; per-listing and per-POCO sites override the default with SnakeCase and KebabCase
    // respectively. WithExplicitOverrides exists to pin that explicit [BsonElement] and [BsonId]
    // beat the policy.
    [BsonSourceGenerationOptions(PropertyNamingPolicy = BsonNamingPolicy.CamelCase)]
    [BsonSerializable(typeof(WithCamelCaseDefault))]
    [BsonSerializable(typeof(WithSnakeCasePerListing), PropertyNamingPolicy = BsonNamingPolicy.SnakeCase)]
    [BsonSerializable(typeof(WithKebabCasePerPoco))]
    [BsonSerializable(typeof(WithExplicitOverrides))]
    public partial class NamingPolicyTestContext : BsonSerializerContext
    {
    }
}
