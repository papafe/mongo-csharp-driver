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

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // C# 11 `required` members — compiler refuses to instantiate via `new AppSettings()` unless
    // every required member is set in an initializer. Generator must therefore emit
    // `new AppSettings() { Theme = ..., FontSize = ... }`.
    public class AppSettings
    {
        public ObjectId Id { get; set; }
        public required string Theme { get; set; }
        public required int FontSize { get; set; }
    }
}
