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
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests;

// Probe: interface HIERARCHIES (IMammal : IAnimal) and MULTIPLE interfaces (Cat : IMammal, IPet),
// simulating Option A by registering the built-in scalar convention on each interface.
public class InterfaceHierarchyProbeTests
{
    static InterfaceHierarchyProbeTests()
    {
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IAnimalH), StandardDiscriminatorConvention.Scalar);
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IMammalH), StandardDiscriminatorConvention.Scalar);
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IPetH), StandardDiscriminatorConvention.Scalar);
    }

    private readonly ITestOutputHelper _output;

    public InterfaceHierarchyProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private IMongoDatabase GetDatabase() =>
        DriverTestConfiguration.Client.GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName);

    private void Report(string label, Func<object> action)
    {
        try { _output.WriteLine($"{label} => {action()}"); }
        catch (Exception ex) { _output.WriteLine($"{label} => THREW {ex.GetType().Name}: {ex.Message}"); }
    }

    [Fact]
    public void Interface_hierarchy_and_multiple_interfaces()
    {
        var db = GetDatabase();
        var animals = db.GetCollection<IAnimalH>("d_hier");
        animals.DeleteMany(Builders<IAnimalH>.Filter.Empty);
        animals.InsertOne(new CatH { Id = ObjectId.GenerateNewId() });
        animals.InsertOne(new DogH { Id = ObjectId.GenerateNewId() });
        animals.InsertOne(new SparrowH { Id = ObjectId.GenerateNewId() });

        _output.WriteLine("WRITE (collection<IAnimalH>):");
        foreach (var d in db.GetCollection<BsonDocument>("d_hier").Find(Builders<BsonDocument>.Filter.Empty).ToList())
        {
            _output.WriteLine("  " + d.ToJson());
        }
        _output.WriteLine("READ: " + string.Join(", ", animals.Find(Builders<IAnimalH>.Filter.Empty).ToList().Select(x => x.GetType().Name)));

        _output.WriteLine("");
        _output.WriteLine("QUERY on collection<IAnimalH>:");
        // concrete target -> walk is on a class, works with Change 1 alone
        Report("  OfType<CatH>() [concrete]", () => animals.Find(Builders<IAnimalH>.Filter.OfType<CatH>()).ToString());
        // intermediate-interface target -> needs Change 2 (IsAssignableFrom); shows the gap today
        Report("  OfType<IMammalH>() [intermediate interface, needs Change 2]",
            () => animals.Find(Builders<IAnimalH>.Filter.OfType<IMammalH>()).ToString());

        _output.WriteLine("");
        _output.WriteLine("MULTIPLE interfaces — same CatH viewed through collection<IPetH>:");
        var petSerializer = BsonSerializer.SerializerRegistry.GetSerializer<IPetH>();
        var petArgs = new RenderArgs<IPetH>(petSerializer, BsonSerializer.SerializerRegistry);
        Report("  Builders<IPetH>.Filter.OfType<CatH>()",
            () => Builders<IPetH>.Filter.OfType<CatH>().Render(petArgs).ToJson());
    }

    // Interface hierarchy: IMammalH : IAnimalH
    public interface IAnimalH { ObjectId Id { get; set; } }
    public interface IMammalH : IAnimalH { }
    public interface IPetH { ObjectId Id { get; set; } }

    public class CatH : IMammalH, IPetH { public ObjectId Id { get; set; } }   // multiple interfaces
    public class DogH : IMammalH { public ObjectId Id { get; set; } }
    public class SparrowH : IAnimalH { public ObjectId Id { get; set; } }       // implements only the base interface
}
