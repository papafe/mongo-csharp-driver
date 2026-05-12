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

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Tiny custom serializer used to verify [BsonSerializer(typeof(X))] passthrough: it uppercases
    // the value on both directions. Any difference vs. the default StringSerializer in the wire
    // bytes proves the generator routed through this type rather than the inline ReadString path.
    public class UpperCaseStringSerializer : SerializerBase<string>
    {
        public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return context.Reader.ReadString().ToUpperInvariant();
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string value)
        {
            context.Writer.WriteString(value?.ToUpperInvariant());
        }
    }
}
