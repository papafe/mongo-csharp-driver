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
    // Locks the post-split architecture: BsonSerializerContext exposes a provider via the Provider
    // property but is not itself an IBsonSerializationProvider. The provider is built lazily by
    // the base class on first access and cached for subsequent calls.
    public class ContextProviderTests
    {
        [Fact]
        public void Context_Type_Does_Not_Implement_The_Provider_Interface()
        {
            // The whole point of the split: the user-facing context is a configuration surface,
            // not a registry-side dispatch mechanism. If this assertion ever fires, the inheritance
            // has crept back and the concerns are conflated again.
            typeof(TestContext).Should().NotImplement<IBsonSerializationProvider>();
        }

        [Fact]
        public void Provider_Is_Stable_Across_Calls()
        {
            var provider1 = TestContext.Default.Provider;
            var provider2 = TestContext.Default.Provider;

            provider1.Should().NotBeNull();
            provider1.Should().BeSameAs(provider2, "Lazy<T> caches the result");
        }

        [Fact]
        public void Provider_Routes_GetSerializer_To_The_Expected_Serializer_Type()
        {
            var provider = TestContext.Default.Provider;

            var serializer = provider.GetSerializer(typeof(SimplePerson));

            serializer.Should().NotBeNull();
            serializer.GetType().Name.Should().Be("SimplePersonSerializer");
        }

        [Fact]
        public void Provider_Returns_Null_For_Unlisted_Type()
        {
            var provider = TestContext.Default.Provider;
            provider.GetSerializer(typeof(string)).Should().BeNull();
        }
    }
}
