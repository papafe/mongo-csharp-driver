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

            // Resolve the context-wide [BsonSourceGenerationOptions] once. Each per-listing and
            // per-POCO layer overrides this default in the layered fold inside ExtractType.
            var contextOptions = ResolveContextOptions(contextSymbol, attrs);

            var types = ImmutableArray.CreateBuilder<TypeToGenerate>(context.Attributes.Length);

            foreach (var attribute in context.Attributes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attribute.ConstructorArguments.Length < 1) { continue; }
                if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol typeArg) { continue; }

                // Per-[BsonSerializable] named-arg override layers on top of context options.
                // A null read at this layer means "no opinion" → keep the inherited value.
                var perListingGuid = AttributeReaders.GetEnumNamedArgument(
                    attribute, "DefaultGuidRepresentation", "GuidRepresentation", unsetValue: 0);
                var perListingNaming = AttributeReaders.GetEnumNamedArgument(
                    attribute, "PropertyNamingPolicy", "BsonNamingPolicy", unsetValue: 0);
                var effective = contextOptions with
                {
                    DefaultGuidRepresentation = perListingGuid ?? contextOptions.DefaultGuidRepresentation,
                    PropertyNamingPolicy = perListingNaming ?? contextOptions.PropertyNamingPolicy,
                };

                var typeInfo = ExtractType(typeArg, attrs, effective, cancellationToken);
                if (typeInfo is not null)
                {
                    types.Add(typeInfo);
                }
            }

            if (types.Count == 0) { return null; }

            return new ContextInfo(
                ContextNamespace: GetNamespace(contextSymbol),
                ContextName: contextSymbol.Name,
                Types: new EquatableArray<TypeToGenerate>(DisambiguateShortNames(types)));
        }

        // Two listed types from different namespaces can share a short name (e.g. `MyApp.Customer`
        // and `Other.Customer`). The emitter names each generated serializer after the short name
        // (`CustomerSerializer`, `s_customerSerializer`), so a collision would produce duplicate
        // identifiers inside the partial context class — CS0102 with no useful pointer at the cause.
        // We rewrite second+ occurrences of any short name with a numeric suffix (Customer,
        // Customer2, Customer3, …) in attribute-declaration order, which is stable across rebuilds.
        // Polymorphic dispatch keys off TypeFullName, so KnownTypes references survive the rename
        // unchanged.
        private static ImmutableArray<TypeToGenerate> DisambiguateShortNames(
            ImmutableArray<TypeToGenerate>.Builder types)
        {
            // Cheap first pass: bail out if no short name appears twice.
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var hasCollision = false;
            foreach (var t in types)
            {
                if (!seen.Add(t.TypeShortName)) { hasCollision = true; break; }
            }
            if (!hasCollision) { return types.ToImmutable(); }

            var counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var result = ImmutableArray.CreateBuilder<TypeToGenerate>(types.Count);
            foreach (var t in types)
            {
                counts.TryGetValue(t.TypeShortName, out var n);
                counts[t.TypeShortName] = n + 1;
                if (n == 0)
                {
                    result.Add(t);
                }
                else
                {
                    var suffixed = t.TypeShortName + (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    result.Add(t with { TypeShortName = suffixed });
                }
            }
            return result.ToImmutable();
        }

        // The compile-time-bakeable options that flow from context → per-[BsonSerializable] →
        // per-POCO override. Null fields mean "no opinion at this layer" — the next layer's value
        // (or the inline / LookupSerializer default) wins. IgnoreExtraElements and IncludeFields
        // are plain bools sourced from the context-wide options only; they're not in the layered
        // fold today (per-type [BsonIgnoreExtraElements] / [BsonIgnore] are the per-type levers).
        // Default constructor must yield the "no context options present" state: include fields
        // (matches reflection) and don't ignore extras.
        private readonly record struct EffectiveOptions(
            string? DefaultGuidRepresentation,
            string? PropertyNamingPolicy,
            bool IgnoreExtraElements,
            bool IncludeFields)
        {
            // "No [BsonSourceGenerationOptions] attribute" baseline. Using `default(EffectiveOptions)`
            // would silently force IncludeFields to false because positional-record defaults are
            // ignored by `default`.
            public static EffectiveOptions Empty => new EffectiveOptions(
                DefaultGuidRepresentation: null,
                PropertyNamingPolicy: null,
                IgnoreExtraElements: false,
                IncludeFields: true);
        }

        // Reads the context-wide [BsonSourceGenerationOptions] attribute on the context class once.
        // Returns Empty when the attribute isn't present or all properties are at their sentinel
        // values.
        private static EffectiveOptions ResolveContextOptions(INamedTypeSymbol contextSymbol, AttributeSymbols attrs)
        {
            if (attrs.SourceGenerationOptions is null) { return EffectiveOptions.Empty; }
            foreach (var a in contextSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrs.SourceGenerationOptions)) { continue; }
                var guid = AttributeReaders.GetEnumNamedArgument(a, "DefaultGuidRepresentation", "GuidRepresentation", unsetValue: 0);
                var naming = AttributeReaders.GetEnumNamedArgument(a, "PropertyNamingPolicy", "BsonNamingPolicy", unsetValue: 0);
                var ignoreExtra = AttributeReaders.GetBoolNamedArgument(a, "IgnoreExtraElements") ?? false;
                var includeFields = AttributeReaders.GetBoolNamedArgument(a, "IncludeFields") ?? true;
                return new EffectiveOptions(
                    DefaultGuidRepresentation: guid,
                    PropertyNamingPolicy: naming,
                    IgnoreExtraElements: ignoreExtra,
                    IncludeFields: includeFields);
            }
            return EffectiveOptions.Empty;
        }

        // [BsonSerializationOverride] on the POCO is the third layer of override. Returns the
        // inherited options unless an explicit override is set.
        private static EffectiveOptions FoldPocoOverride(
            INamedTypeSymbol type,
            AttributeSymbols attrs,
            EffectiveOptions inherited)
        {
            if (attrs.SerializationOverride is null) { return inherited; }
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrs.SerializationOverride)) { continue; }
                var guid = AttributeReaders.GetEnumNamedArgument(a, "DefaultGuidRepresentation", "GuidRepresentation", unsetValue: 0);
                var naming = AttributeReaders.GetEnumNamedArgument(a, "PropertyNamingPolicy", "BsonNamingPolicy", unsetValue: 0);
                return inherited with
                {
                    DefaultGuidRepresentation = guid ?? inherited.DefaultGuidRepresentation,
                    PropertyNamingPolicy = naming ?? inherited.PropertyNamingPolicy,
                };
            }
            return inherited;
        }

        private static TypeToGenerate? ExtractType(
            INamedTypeSymbol type,
            AttributeSymbols attrs,
            EffectiveOptions inheritedOptions,
            CancellationToken cancellationToken)
        {
            var noId = HasAttribute(type, attrs.NoId);

            // Layered fold: context default → per-[BsonSerializable] override → per-POCO
            // [BsonSerializationOverride]. Per-member attributes ([BsonRepresentation],
            // [BsonSerializer]) still win and are handled inside BuildMember.
            var effectiveOptions = FoldPocoOverride(type, attrs, inheritedOptions);

            // [BsonIgnoreExtraElements] on the type is the per-type lever:
            //   - Present with no arg / true  → ignore extras (overrides context).
            //   - Present with false          → strict mode (overrides context).
            //   - Absent                      → fall back to the context-default option.
            // The reflection path's BsonIgnoreExtraElementsAttribute behaves the same way.
            var ignoreExtraElements = ResolveIgnoreExtraElements(type, attrs, effectiveOptions.IgnoreExtraElements);

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
                    if (!TryClassifyMember(member, out var memberType, out var isField, out var isInitOnly, out var isRequired)) { continue; }
                    if (isField && !effectiveOptions.IncludeFields) { continue; }
                    if (HasAttribute(member, attrs.Ignore)) { continue; }
                    if (!seenNames.Add(member.Name)) { continue; } // derived override already added

                    var emitted = BuildMember(member, memberType, isInitOnly, isRequired, type.Name, noId, attrs, effectiveOptions);
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

            var ctorResult = DetermineConstructionStrategy(type);
            if (ctorResult is null) { return null; }

            // Concrete types must have at least one serializable member; an abstract polymorphic
            // root is allowed to have none of its own (its job is to dispatch, not to carry data).
            if (members.Count == 0 && ctorResult.Value.Strategy != ConstructionStrategy.Abstract)
            {
                return null;
            }

            var (discriminator, isRootClass, discriminatorRequired) = ExtractDiscriminator(type, attrs);
            var hasRootAncestor = HasRootAncestor(type, attrs);
            var knownTypes = ExtractKnownTypeFullNames(type, attrs);

            return new TypeToGenerate(
                TypeFullName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                TypeShortName: type.Name,
                IsReferenceType: type.IsReferenceType,
                IgnoreExtraElements: ignoreExtraElements,
                Discriminator: discriminator ?? type.Name,
                WriteDiscriminatorWhenSelf: isRootClass || discriminatorRequired || hasRootAncestor,
                KnownTypeFullNames: knownTypes,
                ConstructionStrategy: ctorResult.Value.Strategy,
                ConstructorParameters: ctorResult.Value.Parameters,
                Members: new EquatableArray<MemberToGenerate>(members.ToImmutable()));
        }

        // True if `member` is a readable+writable property or a non-const, non-readonly field —
        // the only two shapes we can both deserialize into and serialize from. `out` parameters
        // give callers what they need without re-pattern-matching: the member's CLR type, whether
        // it's a field (vs property — drives wire ordering), and the C#-11 init/required flags.
        private static bool TryClassifyMember(
            ISymbol member,
            out ITypeSymbol memberType,
            out bool isField,
            out bool isInitOnly,
            out bool isRequired)
        {
            switch (member)
            {
                case IPropertySymbol p when p.GetMethod is not null && p.SetMethod is not null:
                    memberType = p.Type;
                    isField = false;
                    isInitOnly = p.SetMethod.IsInitOnly;
                    isRequired = p.IsRequired;
                    return true;
                case IFieldSymbol f when !f.IsConst && !f.IsReadOnly:
                    memberType = f.Type;
                    isField = true;
                    isInitOnly = false;
                    isRequired = f.IsRequired;
                    return true;
                default:
                    memberType = null!;
                    isField = false;
                    isInitOnly = false;
                    isRequired = false;
                    return false;
            }
        }

        // Reads every per-member attribute we care about and snapshots the result into a
        // value-equatable `MemberToGenerate`. Pulled out of `ExtractType` so the outer loop
        // stays focused on "should we include this member" decisions; this method only runs
        // once the member is known to be in scope.
        private static MemberToGenerate BuildMember(
            ISymbol member,
            ITypeSymbol memberType,
            bool isInitOnly,
            bool isRequired,
            string declaringTypeName,
            bool typeHasNoId,
            AttributeSymbols attrs,
            EffectiveOptions options)
        {
            var isExtraElements = HasAttribute(member, attrs.ExtraElements);
            var extraElementsKind = ExtraElementsKind.None;
            string? extraElementsCtor = null;
            if (isExtraElements)
            {
                (extraElementsKind, extraElementsCtor) = ClassifyExtraElementsMember(memberType);
            }

            var primitiveKind = ClassifyPrimitive(memberType);
            var representation = GetRepresentationEnumName(member, attrs.Representation);
            var customSerializer = GetAttributeTypeArgument(member, attrs.Serializer);

            // Guid representation precedence (highest first):
            //   1. [BsonSerializer(typeof(X))] — fully replaces the emit; the Guid rep is whatever
            //      the user's serializer does. Falls through this block.
            //   2. [BsonRepresentation(BsonType.X)] — re-encodes the Guid as a different BSON wire
            //      type (e.g. String). Also short-circuits the Guid-rep override; the wire shape
            //      isn't binary so byte order is irrelevant.
            //   3. [BsonGuidRepresentation(GuidRepresentation.X)] — per-member explicit binary
            //      byte ordering. Beats the context default.
            //   4. Context-folded DefaultGuidRepresentation — the layered context → per-listing →
            //      per-POCO fold lives in `options`.
            string? guidRepresentationOverride = null;
            if (primitiveKind == PrimitiveKind.Guid &&
                representation is null &&
                customSerializer is null)
            {
                var perMember = GetGuidRepresentationName(member, attrs.GuidRepresentation);
                guidRepresentationOverride = perMember ?? options.DefaultGuidRepresentation;
            }

            return new MemberToGenerate(
                Name: member.Name,
                ElementName: ResolveElementName(member, member.Name, declaringTypeName, attrs, typeHasNoId, options.PropertyNamingPolicy),
                TypeFullName: memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                PrimitiveKind: primitiveKind,
                AllowsNull: AllowsNull(memberType),
                IsExtraElements: isExtraElements && extraElementsKind != ExtraElementsKind.None,
                ExtraElementsKind: extraElementsKind,
                ExtraElementsConstructor: extraElementsCtor,
                IgnoreIfNull: HasAttribute(member, attrs.IgnoreIfNull),
                IgnoreIfDefault: HasAttribute(member, attrs.IgnoreIfDefault),
                Required: HasAttribute(member, attrs.Required),
                DefaultValueExpression: GetDefaultValueExpression(member, attrs.DefaultValue),
                RepresentationBsonType: representation,
                CustomSerializerTypeFullName: customSerializer,
                GuidRepresentationOverride: guidRepresentationOverride,
                IsInitOnly: isInitOnly,
                IsRequired: isRequired);
        }

        // Mirrors what `BsonClassMap` chooses today (`BsonClassMap.cs:1432-1452`):
        //   1. Public parameterless ctor wins — emit `new T()` and post-construction setters.
        //   2. Otherwise, take the single public instance ctor; emit `new T(p1, p2, ...)` with
        //      members matched to params by case-insensitive name (matches the runtime's
        //      `NamedParameterCreatorMapConvention`).
        //   3. Abstract types are valid as polymorphic roots — we never construct one, so we
        //      return a dedicated strategy that tells the emitter to skip DeserializeCore and
        //      emit pure-dispatch Deserialize / Serialize.
        // If none of those hold (e.g. multiple non-private ctors with no parameterless), we skip
        // the type for v1. A diagnostic in #6 should surface this so users know to add a
        // `[BsonConstructor]` or expose a parameterless ctor.
        private static (ConstructionStrategy Strategy, EquatableArray<CtorParameter> Parameters)? DetermineConstructionStrategy(INamedTypeSymbol type)
        {
            if (type.IsStatic)
            {
                return null;
            }

            if (type.IsAbstract)
            {
                return (ConstructionStrategy.Abstract, new EquatableArray<CtorParameter>(ImmutableArray<CtorParameter>.Empty));
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

        // Per-type [BsonIgnoreExtraElements] beats the context default. Reading order mirrors the
        // reflection-path attribute: a presence-only `[BsonIgnoreExtraElements]` defaults to true;
        // `[BsonIgnoreExtraElements(false)]` opts back to strict mode. Returns the context default
        // when the attribute is absent.
        private static bool ResolveIgnoreExtraElements(
            INamedTypeSymbol type,
            AttributeSymbols attrs,
            bool contextDefault)
        {
            if (attrs.IgnoreExtraElements is null) { return contextDefault; }
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrs.IgnoreExtraElements)) { continue; }
                if (a.ConstructorArguments.Length >= 1 && a.ConstructorArguments[0].Value is bool b)
                {
                    return b;
                }
                return true; // presence-only ctor: `[BsonIgnoreExtraElements]` ≡ true
            }
            return contextDefault;
        }

        private static string ResolveElementName(
            ISymbol member,
            string memberName,
            string declaringTypeName,
            AttributeSymbols attrs,
            bool typeHasNoId,
            string? propertyNamingPolicy)
        {
            // [BsonElement("name")] wins. The user's literal string is honored verbatim — naming
            // policy does *not* re-transform an explicit element name.
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
            // Naming policy does not apply: _id is a wire-protocol identifier, not a renamed member.
            if (!typeHasNoId &&
                (memberName == "Id" || memberName == "id" || memberName == declaringTypeName + "Id"))
            {
                return "_id";
            }

            // Fallback: the member name, optionally transformed by the effective PropertyNamingPolicy.
            return ApplyNamingPolicy(memberName, propertyNamingPolicy);
        }

        // Applies a [BsonSourceGenerationOptions.PropertyNamingPolicy] to a member name at codegen.
        // The string is the enum field name produced by GetEnumNamedArgument (e.g. "CamelCase"); a
        // null or unrecognised policy returns the name unchanged.
        //
        // CamelCase matches the reflection-path CamelCaseElementNameConvention bit-for-bit: lower
        // the first character, keep the rest. SnakeCase / KebabCase use a single word-boundary rule:
        // insert a separator before any upper-case character that's preceded by a lower-case
        // character, or before an upper-case character that's both preceded by an upper-case
        // character *and* followed by a lower-case character (so "URLBuilder" → "url_builder").
        private static string ApplyNamingPolicy(string memberName, string? policy)
        {
            if (string.IsNullOrEmpty(memberName) || policy is null)
            {
                return memberName;
            }

            switch (policy)
            {
                case "CamelCase":
                    if (memberName.Length == 1)
                    {
                        return char.ToLowerInvariant(memberName[0]).ToString();
                    }
                    return char.ToLowerInvariant(memberName[0]) + memberName.Substring(1);

                case "SnakeCase":
                    return ToDelimited(memberName, '_');

                case "KebabCase":
                    return ToDelimited(memberName, '-');

                default:
                    return memberName;
            }
        }

        private static string ToDelimited(string s, char separator)
        {
            var sb = new System.Text.StringBuilder(s.Length + 4);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (i > 0 && char.IsUpper(c))
                {
                    var prev = s[i - 1];
                    var next = i + 1 < s.Length ? s[i + 1] : (char)0;
                    var startsWord = char.IsLower(prev) || (char.IsUpper(prev) && next != 0 && char.IsLower(next));
                    if (startsWord)
                    {
                        sb.Append(separator);
                    }
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
