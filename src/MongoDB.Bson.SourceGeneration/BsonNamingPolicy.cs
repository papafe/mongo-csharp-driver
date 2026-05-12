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
    /// Naming policy applied to BSON element names by the source generator when no
    /// explicit name is provided via <c>[BsonElement]</c>.
    /// </summary>
    public enum BsonNamingPolicy
    {
        /// <summary>
        /// No naming policy. The CLR member name is used as the BSON element name.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// camelCase (e.g. <c>OrderNumber</c> becomes <c>orderNumber</c>).
        /// </summary>
        CamelCase,

        /// <summary>
        /// snake_case (e.g. <c>OrderNumber</c> becomes <c>order_number</c>).
        /// </summary>
        SnakeCase,

        /// <summary>
        /// kebab-case (e.g. <c>OrderNumber</c> becomes <c>order-number</c>).
        /// </summary>
        KebabCase
    }
}
