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

using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Bson.SourceGeneration.Tests
{
    // Abstract polymorphic root. The generator emits an AccountSerializer with the discriminator
    // dispatch but no DeserializeCore — Account can never be instantiated, and case-null /
    // case-"Account" arms throw a BsonSerializationException. SavingsAccount and CheckingAccount
    // inherit the Id/Balance members (the extractor's base-type walk picks them up).
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(SavingsAccount), typeof(CheckingAccount))]
    public abstract class Account
    {
        public ObjectId Id { get; set; }
        public decimal Balance { get; set; }
    }

    public class SavingsAccount : Account
    {
        public double InterestRate { get; set; }
    }

    public class CheckingAccount : Account
    {
        public int OverdraftLimit { get; set; }
    }
}
