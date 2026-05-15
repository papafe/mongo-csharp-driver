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

using Microsoft.CodeAnalysis;

namespace MongoDB.Bson.SourceGeneration.Generator
{
    // Cached `INamedTypeSymbol` lookups for every Bson attribute the extractor reads. Resolved
    // once per Compilation in `Extractor.Extract`; the value-type wrapper threads through the
    // per-type / per-member extraction code without re-querying the symbol table.
    //
    // Lives at file scope (not nested inside Extractor) so other parts of the generator pipeline
    // can use the same lookup without rebuilding it.
    internal readonly struct AttributeSymbols
    {
        private const string BsonAttributesNs = "MongoDB.Bson.Serialization.Attributes.";

        public readonly INamedTypeSymbol? Element;
        public readonly INamedTypeSymbol? Id;
        public readonly INamedTypeSymbol? Ignore;
        public readonly INamedTypeSymbol? IgnoreIfNull;
        public readonly INamedTypeSymbol? IgnoreIfDefault;
        public readonly INamedTypeSymbol? Required;
        public readonly INamedTypeSymbol? DefaultValue;
        public readonly INamedTypeSymbol? ExtraElements;
        public readonly INamedTypeSymbol? Representation;
        public readonly INamedTypeSymbol? GuidRepresentation;
        public readonly INamedTypeSymbol? Serializer;
        public readonly INamedTypeSymbol? IgnoreExtraElements;
        public readonly INamedTypeSymbol? NoId;
        public readonly INamedTypeSymbol? Discriminator;
        public readonly INamedTypeSymbol? KnownTypes;
        public readonly INamedTypeSymbol? Serializable;
        public readonly INamedTypeSymbol? SourceGenerationOptions;
        public readonly INamedTypeSymbol? SerializationOverride;

        private AttributeSymbols(
            INamedTypeSymbol? element,
            INamedTypeSymbol? id,
            INamedTypeSymbol? ignore,
            INamedTypeSymbol? ignoreIfNull,
            INamedTypeSymbol? ignoreIfDefault,
            INamedTypeSymbol? required,
            INamedTypeSymbol? defaultValue,
            INamedTypeSymbol? extraElements,
            INamedTypeSymbol? representation,
            INamedTypeSymbol? guidRepresentation,
            INamedTypeSymbol? serializer,
            INamedTypeSymbol? ignoreExtraElements,
            INamedTypeSymbol? noId,
            INamedTypeSymbol? discriminator,
            INamedTypeSymbol? knownTypes,
            INamedTypeSymbol? serializable,
            INamedTypeSymbol? sourceGenerationOptions,
            INamedTypeSymbol? serializationOverride)
        {
            Element = element;
            Id = id;
            Ignore = ignore;
            IgnoreIfNull = ignoreIfNull;
            IgnoreIfDefault = ignoreIfDefault;
            Required = required;
            DefaultValue = defaultValue;
            ExtraElements = extraElements;
            Representation = representation;
            GuidRepresentation = guidRepresentation;
            Serializer = serializer;
            IgnoreExtraElements = ignoreExtraElements;
            NoId = noId;
            Discriminator = discriminator;
            KnownTypes = knownTypes;
            Serializable = serializable;
            SourceGenerationOptions = sourceGenerationOptions;
            SerializationOverride = serializationOverride;
        }

        public static AttributeSymbols Resolve(Compilation compilation) => new(
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonElementAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonIdAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonIgnoreAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonIgnoreIfNullAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonIgnoreIfDefaultAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonRequiredAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonDefaultValueAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonExtraElementsAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonRepresentationAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonGuidRepresentationAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonSerializerAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonIgnoreExtraElementsAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonNoIdAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonDiscriminatorAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonKnownTypesAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonSerializableAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonSourceGenerationOptionsAttribute"),
            compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonSerializationOverrideAttribute"));
    }
}
