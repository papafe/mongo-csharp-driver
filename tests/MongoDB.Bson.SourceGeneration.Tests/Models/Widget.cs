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
    // Record that implements an interface. The interface is invisible to source-gen — the
    // record is treated like any positional record. The interface only matters if used as a
    // member type or listed in the context; here it's neither.
    public interface IThing
    {
        string Label { get; }
    }

    public record Widget(string Label, int Quantity) : IThing;
}
