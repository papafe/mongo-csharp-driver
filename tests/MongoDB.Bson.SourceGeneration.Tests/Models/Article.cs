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
    // Three-level inheritance with no discriminator. Tests that the extractor's base-type
    // walk handles arbitrary depth: Article should emit Id (from Entity), Title (from
    // Document), and Body (from Article) in that order.
    public class Entity
    {
        public ObjectId Id { get; set; }
    }

    public class Document : Entity
    {
        public string Title { get; set; }
    }

    public class Article : Document
    {
        public string Body { get; set; }
    }
}
