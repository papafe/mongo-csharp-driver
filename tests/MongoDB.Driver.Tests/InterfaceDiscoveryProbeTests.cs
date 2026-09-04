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
using MongoDB.Bson.Serialization.Conventions;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests;

// Probe: under a simulated Option A (scalar convention on the interface), does OfType<Impl> work
// before the implementer has been registered? Demonstrates the "discovery" question behind A vs C.
public class InterfaceDiscoveryProbeTests
{
    static InterfaceDiscoveryProbeTests()
    {
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IThing), StandardDiscriminatorConvention.Scalar);
    }

    private readonly ITestOutputHelper _output;

    public InterfaceDiscoveryProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private void Report(string label, Func<object> action)
    {
        try { _output.WriteLine($"{label} => {action()}"); }
        catch (Exception ex) { _output.WriteLine($"{label} => THREW {ex.GetType().Name}: {ex.Message}"); }
    }

    [Fact]
    public void OfType_before_implementer_is_registered()
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<IThing>();
        var renderArgs = new RenderArgs<IThing>(serializer, BsonSerializer.SerializerRegistry);

        // Alpha has never been inserted, serialized, or RegisterClassMap'd at this point.
        Report("(concrete, not-yet-registered) OfType<Alpha>()",
            () => Builders<IThing>.Filter.OfType<Alpha>().Render(renderArgs).ToJson());

        // Now force Beta to be registered (e.g. as if it had been used), then query it.
        _ = BsonSerializer.SerializerRegistry.GetSerializer<Beta>();
        Report("(concrete, registered) OfType<Beta>()",
            () => Builders<IThing>.Filter.OfType<Beta>().Render(renderArgs).ToJson());
    }

    public interface IThing { ObjectId Id { get; set; } }
    public class Alpha : IThing { public ObjectId Id { get; set; } }
    public class Beta : IThing { public ObjectId Id { get; set; } }
}
