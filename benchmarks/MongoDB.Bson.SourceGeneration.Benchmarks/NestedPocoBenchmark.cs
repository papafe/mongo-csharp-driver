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

using System.IO;
using BenchmarkDotNet.Attributes;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.SourceGeneration.Benchmarks.Models;

namespace MongoDB.Bson.SourceGeneration.Benchmarks
{
    // Exercises the in-context nested-serializer dispatch on the source-gen side. Both members
    // are typed `Address` and Address is also in the context, so the generator emits direct
    // calls on the cached `s_addressSerializer` field — no registry trip. Reflection goes
    // through BsonSerializer.LookupSerializer<Address>() at every nested member access (cached
    // after first use; same cost as the surrounding work though).
    [MemoryDiagnoser]
    public class NestedPocoBenchmark
    {
        private IBsonSerializer<NestedPoco> _reflection = default!;
        private IBsonSerializer<NestedPoco> _sourceGen = default!;

        private NestedPoco _sample = default!;

        private MemoryStream _writeStream = default!;
        private BsonBinaryWriter _writer = default!;
        private BsonSerializationContext _writeCtx = default!;

        private MemoryStream _readStream = default!;
        private BsonBinaryReader _reader = default!;
        private BsonDeserializationContext _readCtx = default!;

        [GlobalSetup]
        public void Setup()
        {
            _reflection = SerializerFactory.BuildReflection<NestedPoco>();
            _sourceGen = SerializerFactory.BuildSourceGen<NestedPoco>();

            _sample = new NestedPoco
            {
                Id = ObjectId.GenerateNewId(),
                CustomerName = "Ada Lovelace",
                ShippingAddress = new Address
                {
                    Street = "1 Lovelace Lane",
                    City = "London",
                    PostalCode = "EC1A 1BB"
                },
                BillingAddress = new Address
                {
                    Street = "1 Babbage Way",
                    City = "London",
                    PostalCode = "EC1A 1CC"
                }
            };

            _writeStream = new MemoryStream(capacity: 512);
            _writer = new BsonBinaryWriter(_writeStream);
            _writeCtx = BsonSerializationContext.CreateRoot(_writer);

            _reflection.Serialize(_writeCtx, _sample);
            var bytes = _writeStream.ToArray();

            _readStream = new MemoryStream(bytes, writable: false);
            _reader = new BsonBinaryReader(_readStream);
            _readCtx = BsonDeserializationContext.CreateRoot(_reader);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _writer.Dispose();
            _writeStream.Dispose();
            _reader.Dispose();
            _readStream.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void Serialize_Reflection()
        {
            _writeStream.Position = 0;
            _reflection.Serialize(_writeCtx, _sample);
        }

        [Benchmark]
        public void Serialize_SourceGen()
        {
            _writeStream.Position = 0;
            _sourceGen.Serialize(_writeCtx, _sample);
        }

        [Benchmark]
        public NestedPoco Deserialize_Reflection()
        {
            _readStream.Position = 0;
            return _reflection.Deserialize(_readCtx);
        }

        [Benchmark]
        public NestedPoco Deserialize_SourceGen()
        {
            _readStream.Position = 0;
            return _sourceGen.Deserialize(_readCtx);
        }
    }
}
