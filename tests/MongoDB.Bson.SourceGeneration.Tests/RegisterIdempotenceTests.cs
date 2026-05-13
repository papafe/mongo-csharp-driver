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
    // Every test class in this project calls TestContext.Default.Register() from its static ctor;
    // before BsonSerializerContext.Register became idempotent, that pushed the same provider onto
    // BsonSerializerRegistry's ConcurrentStack 17 times. Direct dedup verification would require
    // exposing internals — this test just hammers Register() many times and confirms (a) it
    // doesn't throw and (b) lookups still return the right serializer.
    public class RegisterIdempotenceTests
    {
        [Fact]
        public void Repeated_Register_Calls_Do_Not_Throw()
        {
            for (var i = 0; i < 50; i++)
            {
                TestContext.Default.Register();
            }
        }

        [Fact]
        public void Lookup_Still_Resolves_To_Generated_Serializer_After_Repeated_Register()
        {
            for (var i = 0; i < 10; i++)
            {
                TestContext.Default.Register();
            }

            var serializer = BsonSerializer.LookupSerializer<SimplePerson>();
            serializer.GetType().FullName.Should().StartWith("MongoDB.Bson.SourceGeneration.Tests.TestContext");
        }
    }
}
