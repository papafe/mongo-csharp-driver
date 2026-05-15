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
    // Stateless Roslyn-symbol utilities used by the extractor (and potentially the diagnostics
    // analyzer in ticket #6). Pure functions over `ITypeSymbol` / `INamedTypeSymbol` — no
    // dependency on `AttributeSymbols` or extraction state.
    internal static class SymbolHelpers
    {
        // Maps a CLR type to one of the primitives we read/write directly via the BsonReader/Writer.
        // Anything not in `PrimitiveKind` falls back to `BsonSerializer.LookupSerializer<T>()` in
        // emitted code, which preserves parity with the reflection path for nested types.
        public static PrimitiveKind ClassifyPrimitive(ITypeSymbol type)
        {
            return type.SpecialType switch
            {
                SpecialType.System_Boolean => PrimitiveKind.Boolean,
                SpecialType.System_Int32 => PrimitiveKind.Int32,
                SpecialType.System_Int64 => PrimitiveKind.Int64,
                SpecialType.System_Double => PrimitiveKind.Double,
                SpecialType.System_Single => PrimitiveKind.Single,
                SpecialType.System_Decimal => PrimitiveKind.Decimal,
                SpecialType.System_String => PrimitiveKind.String,
                SpecialType.System_DateTime => PrimitiveKind.DateTime,
                _ => ClassifyByFullName(type)
            };
        }

        private static PrimitiveKind ClassifyByFullName(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
            {
                return PrimitiveKind.BinaryData;
            }

            // Any user-defined enum routes through a generic `EnumSerializer<TheEnum>`. The
            // emitter handles the generic argument since it needs the member's CLR type, but
            // classification happens here.
            if (type.TypeKind == TypeKind.Enum)
            {
                return PrimitiveKind.Enum;
            }

            var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return name switch
            {
                "global::MongoDB.Bson.ObjectId" => PrimitiveKind.ObjectId,
                "global::MongoDB.Bson.Decimal128" => PrimitiveKind.Decimal128,
                "global::System.Guid" => PrimitiveKind.Guid,
                _ => PrimitiveKind.None
            };
        }

        // A member "allows null" if its CLR type is a reference type or `Nullable<T>`. Drives
        // whether `[BsonIgnoreIfNull]` gets an actual null-check guard or is a no-op.
        public static bool AllowsNull(ITypeSymbol type)
        {
            if (type.IsReferenceType) { return true; }
            if (type is INamedTypeSymbol named &&
                named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return true;
            }
            return false;
        }

        // Walks the base-type chain looking for `baseType`. Used to confirm a context class
        // actually inherits from `BsonSerializerContext` before treating its `[BsonSerializable]`
        // attributes as extraction roots.
        public static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                {
                    return true;
                }
            }
            return false;
        }

        public static string GetNamespace(INamedTypeSymbol symbol)
        {
            return symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString();
        }

        // True if `type` implements `IDictionary<string, object>`. Used to validate
        // `[BsonExtraElements]` member types and to pick the right runtime adapter on the serialize
        // side (`BsonClassMap.SetExtraElementsMember`, BsonClassMap.cs:1125-1141).
        public static bool ImplementsIDictionaryStringObject(INamedTypeSymbol type)
        {
            foreach (var iface in type.AllInterfaces)
            {
                if (iface.Name != "IDictionary") { continue; }
                if (iface.ContainingNamespace?.ToDisplayString() != "System.Collections.Generic") { continue; }
                if (iface.TypeArguments.Length != 2) { continue; }
                if (iface.TypeArguments[0].SpecialType != SpecialType.System_String) { continue; }
                if (iface.TypeArguments[1].SpecialType != SpecialType.System_Object) { continue; }
                return true;
            }
            return false;
        }

        // True if the type exposes a public parameterless constructor. Used by both the catch-all
        // dictionary classification and the construction-strategy decision.
        public static bool HasParameterlessConstructor(INamedTypeSymbol type)
        {
            foreach (var ctor in type.InstanceConstructors)
            {
                if (ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
