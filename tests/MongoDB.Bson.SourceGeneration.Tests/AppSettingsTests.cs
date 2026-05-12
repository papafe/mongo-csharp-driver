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
using static MongoDB.Bson.SourceGeneration.Tests.SerializationTestHelpers;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    public class AppSettingsTests
    {
        static AppSettingsTests()
        {
            TestContext.Default.Register();
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

        [Fact]
        public void Wire_Format_Matches_Reflection()
        {
            var s = new AppSettings { Id = ObjectId.GenerateNewId(), Theme = "dark", FontSize = 14 };
            var generated = BsonSerializer.LookupSerializer<AppSettings>();
            var reflection = BuildReflectionSerializer<AppSettings>();

            SerializeUsing(generated, s).Should()
                .BeEquivalentTo(SerializeUsing(reflection, s));
        }

        [Fact]
        public void Four_Way_Cross_Deserialization_Round_Trips_Equal_Values()
        {
            var original = new AppSettings { Id = ObjectId.GenerateNewId(), Theme = "dark", FontSize = 14 };
            var generated = BsonSerializer.LookupSerializer<AppSettings>();
            var reflection = BuildReflectionSerializer<AppSettings>();

            var bytesGen = SerializeUsing(generated, original);
            var bytesRefl = SerializeUsing(reflection, original);

            foreach (var (label, value) in new[]
                     {
                         ("gen->gen", DeserializeUsing(generated, bytesGen)),
                         ("gen->refl", DeserializeUsing(reflection, bytesGen)),
                         ("refl->gen", DeserializeUsing(generated, bytesRefl)),
                         ("refl->refl", DeserializeUsing(reflection, bytesRefl)),
                     })
            {
                value.Id.Should().Be(original.Id, label);
                value.Theme.Should().Be("dark", label);
                value.FontSize.Should().Be(14, label);
            }
        }
    }
}
