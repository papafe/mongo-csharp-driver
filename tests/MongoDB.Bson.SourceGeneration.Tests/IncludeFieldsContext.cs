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
    // Context that opts out of public fields. WithFieldsAndProperties has both shapes; only the
    // property should appear in the emitted serializer.
    [BsonSourceGenerationOptions(IncludeFields = false)]
    [BsonSerializable(typeof(WithFieldsAndProperties))]
    public partial class IncludeFieldsContext : BsonSerializerContext
    {
    }
}
