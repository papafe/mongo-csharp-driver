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
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MongoDB.Bson.SourceGeneration.Generator
{
    /// <summary>
    /// Incremental source generator that emits BSON serializers for types listed in classes
    /// deriving from <c>BsonSerializerContext</c>.
    /// </summary>
    [Generator]
    public sealed class BsonSourceGenerator : IIncrementalGenerator
    {
        private const string BsonSerializableAttributeMetadataName =
            "MongoDB.Bson.Serialization.Attributes.BsonSerializableAttribute";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var contexts = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    BsonSerializableAttributeMetadataName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, ct) => Extractor.Extract(ctx, ct))
                .Where(static c => c is not null)
                .Select(static (c, _) => c!);

            context.RegisterSourceOutput(contexts, Emit);
        }

        // Emits one file for the context (partial class + GeneratedProvider preamble) and one
        // file per type's nested `<T>Serializer` class. Split is purely for readability when
        // inspecting generated output; the partial-class assembly produces the same shape the
        // single-file emit did. See PLAN.md "Generated-file layout" for the rationale and a note
        // about reverting to a single file.
        private static void Emit(SourceProductionContext spc, ContextInfo context)
        {
            spc.AddSource(BuildHintName(context, suffix: null), Emitter.EmitContextFile(context));

            foreach (var type in context.Types)
            {
                spc.AddSource(
                    BuildHintName(context, suffix: type.TypeShortName + "Serializer"),
                    Emitter.EmitSerializerFile(context, type));
            }
        }

        // Roslyn requires hint names to be unique per generator. We key on namespace + context name
        // + (for per-type files) the disambiguated TypeShortName, so collisions across contexts or
        // across same-short-name types in different namespaces don't happen.
        private static string BuildHintName(ContextInfo context, string? suffix)
        {
            var prefix = string.IsNullOrEmpty(context.ContextNamespace)
                ? context.ContextName
                : context.ContextNamespace + "." + context.ContextName;
            return suffix is null
                ? prefix + ".g.cs"
                : prefix + "." + suffix + ".g.cs";
        }
    }
}
