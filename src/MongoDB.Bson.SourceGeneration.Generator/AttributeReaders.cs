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

using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace MongoDB.Bson.SourceGeneration.Generator
{
    // Helpers for pulling values out of `AttributeData` and turning primitive `TypedConstant`s
    // into safely-splicable C# literal expressions. Each accepts a nullable attribute symbol so
    // callers can pass in fields from `AttributeSymbols` (which may be null if the attribute type
    // isn't reachable from the compilation) without an extra null check at the call site.
    internal static class AttributeReaders
    {
        // Returns the first `AttributeData` on `symbol` whose class matches `attribute`, or null.
        // Centralises the filter loop that every public reader below would otherwise repeat.
        // A null `attribute` (meaning the attribute type isn't reachable from the compilation)
        // short-circuits to null so callers don't need an extra guard.
        private static AttributeData? FindAttribute(ISymbol symbol, INamedTypeSymbol? attribute)
        {
            if (attribute is null) { return null; }
            foreach (var a in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute)) { return a; }
            }
            return null;
        }

        public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attribute)
            => FindAttribute(symbol, attribute) is not null;

        // Reads the first ctor argument of `[Attribute(typeof(X))]` and returns X's fully-qualified
        // name. Used by `[BsonSerializer(typeof(X))]` to capture the passthrough serializer type.
        public static string? GetAttributeTypeArgument(ISymbol member, INamedTypeSymbol? attribute)
        {
            var a = FindAttribute(member, attribute);
            if (a is null || a.ConstructorArguments.Length < 1) { return null; }
            return a.ConstructorArguments[0].Value is INamedTypeSymbol t
                ? t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null;
        }

        // Reads `[BsonRepresentation(BsonType.X)]` and returns the enum field name ("String",
        // "Int32", etc.) so the emitter can splice `BsonType.<Name>` into a serializer ctor call.
        public static string? GetRepresentationEnumName(ISymbol member, INamedTypeSymbol? attribute)
        {
            var a = FindAttribute(member, attribute);
            if (a is null || a.ConstructorArguments.Length < 1) { return null; }
            var arg = a.ConstructorArguments[0];
            if (arg.Type is not INamedTypeSymbol { Name: "BsonType" } || arg.Value is not int enumValue)
            {
                return null;
            }
            foreach (var field in arg.Type.GetMembers())
            {
                if (field is IFieldSymbol f && f.IsConst && f.ConstantValue is int v && v == enumValue)
                {
                    return f.Name;
                }
            }
            return null;
        }

        public static string? GetDefaultValueExpression(ISymbol member, INamedTypeSymbol? attribute)
        {
            var a = FindAttribute(member, attribute);
            if (a is null || a.ConstructorArguments.Length < 1) { return null; }
            return ToCSharpLiteral(a.ConstructorArguments[0]);
        }

        // Reads a named argument that's typed as an enum and returns the enum-field name (e.g.,
        // "Standard" for `GuidRepresentation.Standard`). Returns null when the named arg is absent
        // or matches the supplied `unsetValue` sentinel (typically the enum's zero member like
        // `GuidRepresentation.Unspecified`) — both mean "no opinion at this layer."
        public static string? GetEnumNamedArgument(
            AttributeData a,
            string argName,
            string enumTypeName,
            int unsetValue)
        {
            foreach (var named in a.NamedArguments)
            {
                if (named.Key != argName) { continue; }
                if (named.Value.Type is not INamedTypeSymbol nt || nt.Name != enumTypeName) { continue; }
                if (named.Value.Value is not int enumValue) { continue; }
                if (enumValue == unsetValue) { return null; }
                foreach (var field in nt.GetMembers())
                {
                    if (field is IFieldSymbol f && f.IsConst && f.ConstantValue is int v && v == enumValue)
                    {
                        return f.Name;
                    }
                }
            }
            return null;
        }

        // Converts a primitive `TypedConstant` into a C# literal expression that's safe to splice
        // into emitted source. v1 covers the primitive types `[BsonDefaultValue]` users actually
        // pass; non-primitive defaults (struct values, enum from a typed constant) return null and
        // are picked up as a diagnostic in ticket #6.
        public static string? ToCSharpLiteral(TypedConstant constant)
        {
            if (constant.IsNull) { return "null"; }
            if (constant.Type is null || constant.Value is null) { return null; }

            switch (constant.Type.SpecialType)
            {
                case SpecialType.System_Boolean: return (bool)constant.Value ? "true" : "false";
                case SpecialType.System_Int32: return ((int)constant.Value).ToString(CultureInfo.InvariantCulture);
                case SpecialType.System_Int64: return ((long)constant.Value).ToString(CultureInfo.InvariantCulture) + "L";
                case SpecialType.System_Double: return ((double)constant.Value).ToString("R", CultureInfo.InvariantCulture) + "D";
                case SpecialType.System_Single: return ((float)constant.Value).ToString("R", CultureInfo.InvariantCulture) + "F";
                case SpecialType.System_String: return "\"" + EscapeStringLiteral((string)constant.Value) + "\"";
                case SpecialType.System_Char: return "'" + EscapeCharLiteral((char)constant.Value) + "'";
                default: return null;
            }
        }

        public static string EscapeStringLiteral(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public static string EscapeCharLiteral(char value)
        {
            return value switch
            {
                '\\' => "\\\\",
                '\'' => "\\'",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\0' => "\\0",
                _ => value.ToString()
            };
        }
    }
}
