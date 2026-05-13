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

using FluentAssertions;
using MongoDB.Bson.Serialization;
using Xunit;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Modern C# member shapes that force the deferred-construction code path:
    //   - Positional records (no parameterless ctor; generator calls primary ctor).
    //   - `init`-only properties (must be set in an object initializer).
    //   - C# 11 `required` members (compiler refuses to instantiate unless set in initializer).
    public class ModernCSharpTests
    {
        static ModernCSharpTests()
        {
            TestContext.Default.Register();
        }

        [Fact]
        public void Positional_Record_Round_Trip()
        {
            var p = new PersonRecord("Ada", 36);
            var bytes = p.ToBson();
            var result = BsonSerializer.Deserialize<PersonRecord>(bytes);

            result.Name.Should().Be("Ada");
            result.Age.Should().Be(36);
        }

        [Fact]
        public void Init_Only_Properties_Round_Trip()
        {
            var p = new AccountProfile
            {
                Id = ObjectId.GenerateNewId(),
                DisplayName = "Ada",
                Bio = "Mathematician"
            };
            var bytes = p.ToBson();
            var result = BsonSerializer.Deserialize<AccountProfile>(bytes);

            result.Id.Should().Be(p.Id);
            result.DisplayName.Should().Be("Ada");
            result.Bio.Should().Be("Mathematician");
        }

        [Fact]
        public void Required_Members_Round_Trip()
        {
            var s = new AppSettings { Id = ObjectId.GenerateNewId(), Theme = "dark", FontSize = 14 };
            var bytes = s.ToBson();
            var result = BsonSerializer.Deserialize<AppSettings>(bytes);

            result.Id.Should().Be(s.Id);
            result.Theme.Should().Be("dark");
            result.FontSize.Should().Be(14);
        }
    }
}
