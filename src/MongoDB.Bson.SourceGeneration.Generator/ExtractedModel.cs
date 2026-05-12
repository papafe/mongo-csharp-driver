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

namespace MongoDB.Bson.SourceGeneration.Generator
{
    // Records that flow through the incremental pipeline. They must be value-equatable
    // (record types over primitives + EquatableArray<T>) so that incremental caching
    // works: when nothing relevant has changed, the pipeline short-circuits.

    internal sealed record ContextInfo(
        string ContextNamespace,
        string ContextName,
        EquatableArray<TypeToGenerate> Types);

    internal sealed record TypeToGenerate(
        string TypeFullName,
        string TypeShortName,
        bool IsReferenceType,
        bool IgnoreExtraElements,
        bool NoId,
        string Discriminator,
        bool WriteDiscriminatorWhenSelf,
        EquatableArray<string> KnownTypeFullNames,
        EquatableArray<MemberToGenerate> Members);

    internal sealed record MemberToGenerate(
        string Name,
        string ElementName,
        string TypeFullName,
        PrimitiveKind PrimitiveKind,
        bool AllowsNull,
        bool IsExtraElements,
        ExtraElementsKind ExtraElementsKind,
        string? ExtraElementsConstructor,
        bool IgnoreIfNull,
        bool IgnoreIfDefault,
        bool Required,
        string? DefaultValueExpression,
        string? RepresentationBsonType,
        string? CustomSerializerTypeFullName);

    // Primitives we read/write directly via BsonReader/BsonWriter without recursing
    // through BsonSerializer.LookupSerializer. Anything not in this enum falls through
    // to LookupSerializer<T>().Serialize/Deserialize at runtime.
    internal enum PrimitiveKind
    {
        None,
        Boolean,
        Int32,
        Int64,
        Double,
        String,
        ObjectId,
        DateTime,
        Decimal128,
        Guid,
        BinaryData
    }

    // Shape of a [BsonExtraElements] catch-all member. The two supported shapes mirror
    // BsonClassMap.SetExtraElementsMember validation: BsonDocument, or any type implementing
    // IDictionary<string, object>.
    internal enum ExtraElementsKind
    {
        None,
        BsonDocument,
        Dictionary
    }
}
