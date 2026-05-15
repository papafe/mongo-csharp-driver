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

namespace MongoDB.Bson.Serialization
{
    /// <summary>
    /// A naming policy that the source generator applies at compile time when computing the wire
    /// name for a member that has no explicit <c>[BsonElement]</c>. The policy is read from
    /// <c>BsonSourceGenerationOptionsAttribute.PropertyNamingPolicy</c> (or its per-listing /
    /// per-POCO override siblings) and baked into the emitted serializer — no runtime convention
    /// runs.
    /// </summary>
    public enum BsonNamingPolicy
    {
        /// <summary>
        /// No opinion at this layer. The next layer in the fold wins; if no layer sets a policy
        /// the member name is emitted verbatim.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Lower-case the first character of the member name; the rest is unchanged. Matches the
        /// reflection-path <c>CamelCaseElementNameConvention</c> exactly so wire output is
        /// identical whether a user opts into source-gen or stays on conventions.
        /// </summary>
        CamelCase = 1,

        /// <summary>
        /// Insert underscores at word boundaries and lower-case the result. Word boundaries are
        /// detected between a lower-case character and an upper-case character, and between two
        /// upper-case characters when the second is followed by a lower-case character (so
        /// <c>"URLBuilder"</c> becomes <c>"url_builder"</c>, not <c>"u_r_l_builder"</c>).
        /// </summary>
        SnakeCase = 2,

        /// <summary>
        /// Same rule as <see cref="SnakeCase"/>, but the separator is a hyphen rather than an
        /// underscore.
        /// </summary>
        KebabCase = 3
    }
}
