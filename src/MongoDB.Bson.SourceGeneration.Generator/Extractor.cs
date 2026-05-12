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
using System.Threading;
using Microsoft.CodeAnalysis;
using static MongoDB.Bson.SourceGeneration.Generator.AttributeReaders;
using static MongoDB.Bson.SourceGeneration.Generator.SymbolHelpers;

namespace MongoDB.Bson.SourceGeneration.Generator
{
    // Pulls ContextInfo out of a class decorated with [BsonSerializable]. Skips classes that
    // don't derive from BsonSerializerContext; we'll report a diagnostic for that in ticket #6.
    //
    // The stateless helpers used here live in sibling files:
    //   - AttributeSymbols       : cached INamedTypeSymbol lookups for the Bson attributes.
    //   - AttributeReaders       : pull values out of an AttributeData (with safe escaping).
    //   - SymbolHelpers          : Roslyn-symbol utilities (type classification, inheritance...).
    internal static class Extractor
    {
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
                    var isInitOnly = false;
                    var isRequired = false;

                    switch (member)
                    {
                        case IPropertySymbol p when p.GetMethod is not null && p.SetMethod is not null:
                            memberType = p.Type;
                            isInitOnly = p.SetMethod.IsInitOnly;
                            isRequired = p.IsRequired;
                            break;
                        case IFieldSymbol f when !f.IsConst && !f.IsReadOnly:
                            memberType = f.Type;
                            isField = true;
                            isRequired = f.IsRequired;
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
                        CustomSerializerTypeFullName: customSerializerType,
                        IsInitOnly: isInitOnly,
                        IsRequired: isRequired);

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

            var ctorResult = DetermineConstructionStrategy(type);
            if (ctorResult is null) { return null; }

            return new TypeToGenerate(
                TypeFullName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                TypeShortName: type.Name,
                IsReferenceType: type.IsReferenceType,
                IgnoreExtraElements: ignoreExtraElements,
                NoId: noId,
                Discriminator: discriminator ?? type.Name,
                WriteDiscriminatorWhenSelf: isRootClass || discriminatorRequired || hasRootAncestor,
                KnownTypeFullNames: knownTypes,
                ConstructionStrategy: ctorResult.Value.Strategy,
                ConstructorParameters: ctorResult.Value.Parameters,
                Members: new EquatableArray<MemberToGenerate>(members.ToImmutable()));
        }

        // Mirrors what `BsonClassMap` chooses today (`BsonClassMap.cs:1432-1452`):
        //   1. Public parameterless ctor wins — emit `new T()` and post-construction setters.
        //   2. Otherwise, take the single public instance ctor; emit `new T(p1, p2, ...)` with
        //      members matched to params by case-insensitive name (matches the runtime's
        //      `NamedParameterCreatorMapConvention`).
        // If neither holds (e.g. multiple non-private ctors with no parameterless), we skip the
        // type for v1. A diagnostic in #6 should surface this so users know to add a
        // `[BsonConstructor]` or expose a parameterless ctor.
        private static (ConstructionStrategy Strategy, EquatableArray<CtorParameter> Parameters)? DetermineConstructionStrategy(INamedTypeSymbol type)
        {
            if (type.IsAbstract || type.IsStatic)
            {
                return null;
            }

            IMethodSymbol? parameterless = null;
            var publicCtors = new List<IMethodSymbol>();
            foreach (var ctor in type.InstanceConstructors)
            {
                if (ctor.DeclaredAccessibility != Accessibility.Public) { continue; }
                if (ctor.Parameters.Length == 0)
                {
                    parameterless = ctor;
                }
                else
                {
                    publicCtors.Add(ctor);
                }
            }

            if (parameterless is not null)
            {
                return (ConstructionStrategy.Parameterless, new EquatableArray<CtorParameter>(ImmutableArray<CtorParameter>.Empty));
            }

            if (publicCtors.Count == 1)
            {
                var ctor = publicCtors[0];
                var paramsBuilder = ImmutableArray.CreateBuilder<CtorParameter>(ctor.Parameters.Length);
                foreach (var p in ctor.Parameters)
                {
                    paramsBuilder.Add(new CtorParameter(
                        Name: p.Name,
                        TypeFullName: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
                return (ConstructionStrategy.ParameterizedCtor, new EquatableArray<CtorParameter>(paramsBuilder.ToImmutable()));
            }

            return null;
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
    }
}
