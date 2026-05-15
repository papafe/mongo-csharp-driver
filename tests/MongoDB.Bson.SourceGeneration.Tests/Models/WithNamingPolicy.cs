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
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // POCOs that exercise PropertyNamingPolicy across the layered fold. Member names are
    // deliberately mixed-shape so each policy produces a distinct, easy-to-recognise wire name:
    //   FirstName  → camelCase: firstName, snake_case: first_name, kebab-case: first-name
    //   URLBuilder → camelCase: uRLBuilder (matches the existing CamelCaseElementNameConvention
    //                rule: lower the first char only), snake_case: url_builder, kebab-case: url-builder
    //   ID         → camelCase: iD, snake_case: id, kebab-case: id
    public class WithCamelCaseDefault
    {
        public string FirstName { get; set; }
        public string URLBuilder { get; set; }
        public string ID { get; set; }
    }

    // Inherits camelCase from the context, then overrides to snake_case at the [BsonSerializable]
    // listing site.
    public class WithSnakeCasePerListing
    {
        public string FirstName { get; set; }
        public string URLBuilder { get; set; }
    }

    // Inherits camelCase from the context, then overrides to kebab-case via the POCO-level
    // [BsonSerializationOverride].
    [BsonSerializationOverride(PropertyNamingPolicy = BsonNamingPolicy.KebabCase)]
    public class WithKebabCasePerPoco
    {
        public string FirstName { get; set; }
        public string URLBuilder { get; set; }
    }

    // Pins the precedence rules:
    //   - [BsonElement("explicit")] wins over the naming policy.
    //   - [BsonId] / the default Id convention always maps to _id, never policy-transformed.
    public class WithExplicitOverrides
    {
        [BsonId]
        public string MyKey { get; set; }

        [BsonElement("legacy_explicit_name")]
        public string FirstName { get; set; }
    }
}
