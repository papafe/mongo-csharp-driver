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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace MongoDB.Bson.SourceGeneration.Generator
{
    // Pulls ContextInfo out of a class decorated with [BsonSerializable]. Skips
    // classes that don't derive from BsonSerializerContext; we'll report a
    // diagnostic for that in ticket #6.
    internal static class Extractor
    {
        private const string BsonAttributesNs = "MongoDB.Bson.Serialization.Attributes.";

        private const string BsonSerializerContextMetadataName = "MongoDB.Bson.Serialization.BsonSerializerContext";

        public static ContextInfo? Extract(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.TargetSymbol is not INamedTypeSymbol contextSymbol)
            {
                return null;
            }

            var compilation = context.SemanticModel.Compilation;
            var contextBase = compilation.GetTypeByMetadataName(BsonSerializerContextMetadataName);
            if (contextBase is null || !InheritsFrom(contextSymbol, contextBase))
            {
                return null;
            }

            var attrs = AttributeSymbols.Resolve(compilation);

            var types = ImmutableArray.CreateBuilder<TypeToGenerate>(context.Attributes.Length);

            foreach (var attribute in context.Attributes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attribute.ConstructorArguments.Length < 1) { continue; }
                if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol typeArg) { continue; }

                var typeInfo = ExtractType(typeArg, attrs, cancellationToken);
                if (typeInfo is not null)
                {
                    types.Add(typeInfo);
                }
            }

            if (types.Count == 0) { return null; }

            return new ContextInfo(
                ContextNamespace: GetNamespace(contextSymbol),
                ContextName: contextSymbol.Name,
                Types: new EquatableArray<TypeToGenerate>(types.ToImmutable()));
        }

        private static TypeToGenerate? ExtractType(
            INamedTypeSymbol type,
            AttributeSymbols attrs,
            CancellationToken cancellationToken)
        {
            var ignoreExtraElements = HasAttribute(type, attrs.IgnoreExtraElements);
            var noId = HasAttribute(type, attrs.NoId);

            // Walk derived → base, collecting one (fields, properties) pair per level. Names already
            // contributed by a more-derived level win (matches `new`/override semantics; first-seen
            // here is most-derived because the walk starts at `type`). Levels are reversed at the end
            // so the final flat list is [root.fields, root.props, ..., leaf.fields, leaf.props], which
            // is exactly what `BsonClassMap.AllMemberMaps` produces with the default conventions.
            var levels = new List<(ImmutableArray<MemberToGenerate>.Builder Fields, ImmutableArray<MemberToGenerate>.Builder Properties)>();
            var seenNames = new HashSet<string>();
            for (var current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fields = ImmutableArray.CreateBuilder<MemberToGenerate>();
                var properties = ImmutableArray.CreateBuilder<MemberToGenerate>();

                foreach (var member in current.GetMembers())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (member.IsStatic) { continue; }
                    if (member.DeclaredAccessibility != Accessibility.Public) { continue; }

                    ITypeSymbol? memberType = null;
                    var memberName = member.Name;
                    var isField = false;

                    switch (member)
                    {
                        case IPropertySymbol p when p.GetMethod is not null && p.SetMethod is not null:
                            memberType = p.Type;
                            break;
                        case IFieldSymbol f when !f.IsConst && !f.IsReadOnly:
                            memberType = f.Type;
                            isField = true;
                            break;
                        default:
                            continue;
                    }

                    if (HasAttribute(member, attrs.Ignore)) { continue; }
                    if (!seenNames.Add(memberName)) { continue; } // derived override already added

                    var elementName = ResolveElementName(member, memberName, type.Name, attrs, noId);

                    var customSerializerType = GetAttributeTypeArgument(member, attrs.Serializer);
                    var representation = GetRepresentationEnumName(member, attrs.Representation);
                    var defaultValueExpr = GetDefaultValueExpression(member, attrs.DefaultValue);
                    var isExtraElements = HasAttribute(member, attrs.ExtraElements);
                    var extraElementsKind = ExtraElementsKind.None;
                    string? extraElementsCtor = null;
                    if (isExtraElements)
                    {
                        (extraElementsKind, extraElementsCtor) = ClassifyExtraElementsMember(memberType);
                    }

                    var emitted = new MemberToGenerate(
                        Name: memberName,
                        ElementName: elementName,
                        TypeFullName: memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        PrimitiveKind: ClassifyPrimitive(memberType),
                        AllowsNull: AllowsNull(memberType),
                        IsExtraElements: isExtraElements && extraElementsKind != ExtraElementsKind.None,
                        ExtraElementsKind: extraElementsKind,
                        ExtraElementsConstructor: extraElementsCtor,
                        IgnoreIfNull: HasAttribute(member, attrs.IgnoreIfNull),
                        IgnoreIfDefault: HasAttribute(member, attrs.IgnoreIfDefault),
                        Required: HasAttribute(member, attrs.Required),
                        DefaultValueExpression: defaultValueExpr,
                        RepresentationBsonType: representation,
                        CustomSerializerTypeFullName: customSerializerType);

                    (isField ? fields : properties).Add(emitted);
                }

                levels.Add((fields, properties));
            }

            levels.Reverse();
            var members = ImmutableArray.CreateBuilder<MemberToGenerate>();
            foreach (var level in levels)
            {
                members.AddRange(level.Fields);
                members.AddRange(level.Properties);
            }

            if (members.Count == 0) { return null; }

            var (discriminator, isRootClass, discriminatorRequired) = ExtractDiscriminator(type, attrs);
            var hasRootAncestor = HasRootAncestor(type, attrs);
            var knownTypes = ExtractKnownTypeFullNames(type, attrs);

            return new TypeToGenerate(
                TypeFullName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                TypeShortName: type.Name,
                IsReferenceType: type.IsReferenceType,
                IgnoreExtraElements: ignoreExtraElements,
                NoId: noId,
                Discriminator: discriminator ?? type.Name,
                WriteDiscriminatorWhenSelf: isRootClass || discriminatorRequired || hasRootAncestor,
                KnownTypeFullNames: knownTypes,
                Members: new EquatableArray<MemberToGenerate>(members.ToImmutable()));
        }

        // [BsonDiscriminator] on a type:
        //   - Optional ctor arg sets the explicit discriminator string (otherwise we default to Type.Name).
        //   - Named arg Required=true → write _t even when actualType == nominalType.
        //   - Named arg RootClass=true → this type is the top of a polymorphic hierarchy; descendants
        //     should write _t. We propagate the effect via HasRootAncestor on subtypes.
        private static (string? Discriminator, bool IsRootClass, bool Required) ExtractDiscriminator(
            INamedTypeSymbol type,
            AttributeSymbols attrs)
        {
            if (attrs.Discriminator is null) { return (null, false, false); }
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrs.Discriminator)) { continue; }

                string? value = null;
                if (a.ConstructorArguments.Length >= 1 && a.ConstructorArguments[0].Value is string s)
                {
                    value = s;
                }

                var required = false;
                var rootClass = false;
                foreach (var named in a.NamedArguments)
                {
                    switch (named.Key)
                    {
                        case "Required" when named.Value.Value is bool rb: required = rb; break;
                        case "RootClass" when named.Value.Value is bool rcb: rootClass = rcb; break;
                    }
                }

                return (value, rootClass, required);
            }
            return (null, false, false);
        }

        // A subtype must write _t if any ancestor was marked [BsonDiscriminator(RootClass=true)]. We
        // don't propagate Required here — that's a property of the marked type itself, not the chain.
        private static bool HasRootAncestor(INamedTypeSymbol type, AttributeSymbols attrs)
        {
            if (attrs.Discriminator is null) { return false; }
            for (var ancestor = type.BaseType; ancestor is not null && ancestor.SpecialType != SpecialType.System_Object; ancestor = ancestor.BaseType)
            {
                foreach (var a in ancestor.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrs.Discriminator)) { continue; }
                    foreach (var named in a.NamedArguments)
                    {
                        if (named.Key == "RootClass" && named.Value.Value is bool b && b) { return true; }
                    }
                }
            }
            return false;
        }

        private static EquatableArray<string> ExtractKnownTypeFullNames(INamedTypeSymbol type, AttributeSymbols attrs)
        {
            if (attrs.KnownTypes is null) { return new EquatableArray<string>(ImmutableArray<string>.Empty); }

            var builder = ImmutableArray.CreateBuilder<string>();
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrs.KnownTypes)) { continue; }
                if (a.ConstructorArguments.Length < 1) { continue; }

                var arg = a.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var element in arg.Values)
                    {
                        if (element.Value is INamedTypeSymbol t)
                        {
                            builder.Add(t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        }
                    }
                }
                else if (arg.Value is INamedTypeSymbol single)
                {
                    builder.Add(single.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
            }
            return new EquatableArray<string>(builder.ToImmutable());
        }

        // Mirrors BsonClassMap.SetExtraElementsMember (BsonClassMap.cs:1125-1141): the catch-all
        // member must be BsonDocument or implement IDictionary<string, object>. For the interface
        // itself the runtime allocates a Dictionary<string, object>; for a concrete dictionary type
        // it uses Activator.CreateInstance — at codegen time we emit a `new T()` directly.
        private static (ExtraElementsKind kind, string? ctor) ClassifyExtraElementsMember(ITypeSymbol memberType)
        {
            var fullName = memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullName == "global::MongoDB.Bson.BsonDocument")
            {
                return (ExtraElementsKind.BsonDocument, "new global::MongoDB.Bson.BsonDocument()");
            }

            if (fullName == "global::System.Collections.Generic.IDictionary<string, object>" ||
                fullName == "global::System.Collections.Generic.IDictionary<string, object?>")
            {
                return (ExtraElementsKind.Dictionary, "new global::System.Collections.Generic.Dictionary<string, object>()");
            }

            // Concrete type: must implement IDictionary<string, object> and have a parameterless ctor.
            if (memberType is INamedTypeSymbol named &&
                ImplementsIDictionaryStringObject(named) &&
                HasParameterlessConstructor(named))
            {
                return (ExtraElementsKind.Dictionary, "new " + fullName + "()");
            }

            // Not a supported shape — fall back to "no extra-elements member". A diagnostic will
            // call this out in ticket #6; for now the member is treated like any other and will
            // fail the unknown-element check during deserialize.
            return (ExtraElementsKind.None, null);
        }

        private static bool ImplementsIDictionaryStringObject(INamedTypeSymbol type)
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

        private static bool HasParameterlessConstructor(INamedTypeSymbol type)
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

        private static string ResolveElementName(
            ISymbol member,
            string memberName,
            string declaringTypeName,
            AttributeSymbols attrs,
            bool typeHasNoId)
        {
            // [BsonElement("name")] wins.
            if (attrs.Element is not null)
            {
                foreach (var a in member.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrs.Element) &&
                        a.ConstructorArguments.Length >= 1 &&
                        a.ConstructorArguments[0].Value is string explicitName &&
                        !string.IsNullOrEmpty(explicitName))
                    {
                        return explicitName;
                    }
                }
            }

            // [BsonId] maps to _id, regardless of [BsonNoId] on the type (explicit attribute beats type-wide convention suppression).
            if (HasAttribute(member, attrs.Id))
            {
                return "_id";
            }

            // Default Id-detection convention: Id, id, <ClassName>Id — suppressed if [BsonNoId] on the type.
            if (!typeHasNoId &&
                (memberName == "Id" || memberName == "id" || memberName == declaringTypeName + "Id"))
            {
                return "_id";
            }

            // Otherwise: the member name verbatim. Naming policies (camelCase, etc.) come later.
            return memberName;
        }

        private static PrimitiveKind ClassifyPrimitive(ITypeSymbol type)
        {
            return type.SpecialType switch
            {
                SpecialType.System_Boolean => PrimitiveKind.Boolean,
                SpecialType.System_Int32 => PrimitiveKind.Int32,
                SpecialType.System_Int64 => PrimitiveKind.Int64,
                SpecialType.System_Double => PrimitiveKind.Double,
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

            var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return name switch
            {
                "global::MongoDB.Bson.ObjectId" => PrimitiveKind.ObjectId,
                "global::MongoDB.Bson.Decimal128" => PrimitiveKind.Decimal128,
                "global::System.Guid" => PrimitiveKind.Guid,
                _ => PrimitiveKind.None
            };
        }

        private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attribute)
        {
            if (attribute is null) { return false; }
            foreach (var a in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute)) { return true; }
            }
            return false;
        }

        // A member "allows null" if its CLR type is a reference type or Nullable<T>.
        // Drives whether [BsonIgnoreIfNull] gets an actual null-check guard or is a no-op.
        private static bool AllowsNull(ITypeSymbol type)
        {
            if (type.IsReferenceType) { return true; }
            if (type is INamedTypeSymbol named &&
                named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return true;
            }
            return false;
        }

        private static string? GetAttributeTypeArgument(ISymbol member, INamedTypeSymbol? attribute)
        {
            if (attribute is null) { return null; }
            foreach (var a in member.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute)) { continue; }
                if (a.ConstructorArguments.Length < 1) { continue; }
                if (a.ConstructorArguments[0].Value is INamedTypeSymbol t)
                {
                    return t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }
            }
            return null;
        }

        private static string? GetRepresentationEnumName(ISymbol member, INamedTypeSymbol? attribute)
        {
            // [BsonRepresentation(BsonType.X)] — capture the enum field name ("String", "Int32", etc.).
            if (attribute is null) { return null; }
            foreach (var a in member.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute)) { continue; }
                if (a.ConstructorArguments.Length < 1) { continue; }
                var arg = a.ConstructorArguments[0];
                if (arg.Type is INamedTypeSymbol { Name: "BsonType" } && arg.Value is int enumValue)
                {
                    // Find the matching field name on the enum
                    foreach (var field in arg.Type.GetMembers())
                    {
                        if (field is IFieldSymbol f && f.IsConst && f.ConstantValue is int v && v == enumValue)
                        {
                            return f.Name;
                        }
                    }
                }
            }
            return null;
        }

        private static string? GetDefaultValueExpression(ISymbol member, INamedTypeSymbol? attribute)
        {
            if (attribute is null) { return null; }
            foreach (var a in member.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute)) { continue; }
                if (a.ConstructorArguments.Length < 1) { continue; }
                return ToCSharpLiteral(a.ConstructorArguments[0]);
            }
            return null;
        }

        // Converts a primitive TypedConstant into a C# literal expression that's safe to
        // splice into emitted source. We don't try to handle non-primitive defaults in v1
        // (e.g., struct values, enum from a typed constant); those would surface as a
        // diagnostic in ticket #6.
        private static string? ToCSharpLiteral(TypedConstant constant)
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

        private static string EscapeStringLiteral(string value)
        {
            var sb = new System.Text.StringBuilder(value.Length);
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

        private static string EscapeCharLiteral(char value)
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

        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
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

        private static string GetNamespace(INamedTypeSymbol symbol)
        {
            return symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString();
        }

        // Cached lookups of the attribute types in the current Compilation. INamedTypeSymbol
        // values stay scoped to the Extract call; they never flow through the incremental cache.
        private readonly struct AttributeSymbols
        {
            public readonly INamedTypeSymbol? Element;
            public readonly INamedTypeSymbol? Id;
            public readonly INamedTypeSymbol? Ignore;
            public readonly INamedTypeSymbol? IgnoreIfNull;
            public readonly INamedTypeSymbol? IgnoreIfDefault;
            public readonly INamedTypeSymbol? Required;
            public readonly INamedTypeSymbol? DefaultValue;
            public readonly INamedTypeSymbol? ExtraElements;
            public readonly INamedTypeSymbol? Representation;
            public readonly INamedTypeSymbol? Serializer;
            public readonly INamedTypeSymbol? IgnoreExtraElements;
            public readonly INamedTypeSymbol? NoId;
            public readonly INamedTypeSymbol? Discriminator;
            public readonly INamedTypeSymbol? KnownTypes;

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
                INamedTypeSymbol? serializer,
                INamedTypeSymbol? ignoreExtraElements,
                INamedTypeSymbol? noId,
                INamedTypeSymbol? discriminator,
                INamedTypeSymbol? knownTypes)
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
                Serializer = serializer;
                IgnoreExtraElements = ignoreExtraElements;
                NoId = noId;
                Discriminator = discriminator;
                KnownTypes = knownTypes;
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
                compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonSerializerAttribute"),
                compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonIgnoreExtraElementsAttribute"),
                compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonNoIdAttribute"),
                compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonDiscriminatorAttribute"),
                compilation.GetTypeByMetadataName(BsonAttributesNs + "BsonKnownTypesAttribute"));
        }
    }
}
