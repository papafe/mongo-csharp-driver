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

using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Bson.SourceGeneration.AotTests
{
    public class SimplePerson
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    [BsonSerializable(typeof(SimplePerson))]
    public partial class AotTestContext : BsonSerializerContext
    {
    }

    internal static class Program
    {
        private static int Main()
        {
            AotTestContext.Default.Register();

            var original = new SimplePerson
            {
                Id = ObjectId.GenerateNewId(),
                Name = "Ada Lovelace",
                Age = 36
            };

            var bytes = original.ToBson();
            var roundTripped = BsonSerializer.Deserialize<SimplePerson>(bytes);

            if (roundTripped.Id != original.Id ||
                roundTripped.Name != original.Name ||
                roundTripped.Age != original.Age)
            {
                Console.Error.WriteLine("Round-trip failed.");
                return 1;
            }

            Console.WriteLine($"AOT round-trip OK: {roundTripped.Name}, age {roundTripped.Age}, id {roundTripped.Id}");
            return 0;
        }
    }
}
