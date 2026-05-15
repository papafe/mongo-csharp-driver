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
    // Steady-state per-op cost of (de)serializing a small primitive-only POCO with both paths.
    // Reflection is the baseline; the source-gen ratio is what matters. Streams + readers/writers
    // are reused across iterations (position reset to 0) so the only allocations BenchmarkDotNet
    // sees are the ones the serializer itself produces.
    [MemoryDiagnoser]
    public class SimplePocoBenchmark
    {
        private IBsonSerializer<SimplePoco> _reflection = default!;
        private IBsonSerializer<SimplePoco> _sourceGen = default!;

        private SimplePoco _sample = default!;

        private MemoryStream _writeStream = default!;
        private BsonBinaryWriter _writer = default!;
        private BsonSerializationContext _writeCtx = default!;

        private MemoryStream _readStream = default!;
        private BsonBinaryReader _reader = default!;
        private BsonDeserializationContext _readCtx = default!;

        [GlobalSetup]
        public void Setup()
        {
            _reflection = SerializerFactory.BuildReflection<SimplePoco>();
            _sourceGen = SerializerFactory.BuildSourceGen<SimplePoco>();

            _sample = new SimplePoco
            {
                Id = ObjectId.GenerateNewId(),
                Name = "Ada Lovelace",
                Age = 35,
                IsActive = true,
                Score = 42.5
            };

            _writeStream = new MemoryStream(capacity: 256);
            _writer = new BsonBinaryWriter(_writeStream);
            _writeCtx = BsonSerializationContext.CreateRoot(_writer);

            // Capture canonical bytes from one serialize (either path; output is byte-identical)
            // and feed them into a backing stream the read benchmarks rewind.
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
        public SimplePoco Deserialize_Reflection()
        {
            _readStream.Position = 0;
            return _reflection.Deserialize(_readCtx);
        }

        [Benchmark]
        public SimplePoco Deserialize_SourceGen()
        {
            _readStream.Position = 0;
            return _sourceGen.Deserialize(_readCtx);
        }
    }
}
