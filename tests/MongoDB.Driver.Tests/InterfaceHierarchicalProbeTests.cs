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
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests;

// Probe: does hierarchical work through an interface today, and is scalar the only option under Option A?
// We SIMULATE Option A by registering a convention for the interface ourselves (which a user can do today).
public class InterfaceHierarchicalProbeTests
{
    static InterfaceHierarchicalProbeTests()
    {
        // Scenario 1: register the built-in SCALAR convention for the interface (this is Option A's new default).
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IGadgetScalar), StandardDiscriminatorConvention.Scalar);
        // Scenario 2: register the built-in HIERARCHICAL convention for the interface (a user opting in).
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IGadgetHier), StandardDiscriminatorConvention.Hierarchical);
    }

    private readonly ITestOutputHelper _output;

    public InterfaceHierarchicalProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private IMongoDatabase GetDatabase() =>
        DriverTestConfiguration.Client.GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName);

    private void Report(string label, Func<object> action)
    {
        try
        {
            _output.WriteLine($"{label} => {action()}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"{label} => THREW {ex.GetType().Name}: {ex.Message}");
        }
    }

    [Fact]
    public void Scalar_interface_over_rootclass_hierarchy()
    {
        var db = GetDatabase();
        var col = db.GetCollection<IGadgetScalar>("d_gadget_scalar");
        col.DeleteMany(Builders<IGadgetScalar>.Filter.Empty);
        col.InsertOne(new PhoneS { Id = ObjectId.GenerateNewId() });
        col.InsertOne(new TabletS { Id = ObjectId.GenerateNewId() });

        _output.WriteLine("== interface convention = SCALAR, concrete hierarchy has a ROOT CLASS ==");
        _output.WriteLine("WRITE (note _t shape is driven by the CONCRETE class, not the interface):");
        foreach (var d in db.GetCollection<BsonDocument>("d_gadget_scalar").Find(Builders<BsonDocument>.Filter.Empty).ToList())
        {
            _output.WriteLine("  " + d.ToJson());
        }
        _output.WriteLine("READ: " + string.Join(", ", col.Find(Builders<IGadgetScalar>.Filter.Empty).ToList().Select(x => x.GetType().Name)));
        Report("QUERY OfType<PhoneS>()", () => col.Find(Builders<IGadgetScalar>.Filter.OfType<PhoneS>()).ToString());
        Report("QUERY OfType<PhoneS> result count", () => col.OfType<PhoneS>().Find(Builders<PhoneS>.Filter.Empty).ToList().Count);
    }

    [Fact]
    public void Hierarchical_interface_over_rootclass_hierarchy()
    {
        var db = GetDatabase();
        var col = db.GetCollection<IGadgetHier>("d_gadget_hier");
        col.DeleteMany(Builders<IGadgetHier>.Filter.Empty);
        col.InsertOne(new PhoneH { Id = ObjectId.GenerateNewId() });
        col.InsertOne(new TabletH { Id = ObjectId.GenerateNewId() });

        _output.WriteLine("== interface convention = HIERARCHICAL ==");
        _output.WriteLine("WRITE:");
        foreach (var d in db.GetCollection<BsonDocument>("d_gadget_hier").Find(Builders<BsonDocument>.Filter.Empty).ToList())
        {
            _output.WriteLine("  " + d.ToJson());
        }
        _output.WriteLine("READ: " + string.Join(", ", col.Find(Builders<IGadgetHier>.Filter.Empty).ToList().Select(x => x.GetType().Name)));
        Report("QUERY OfType<PhoneH>()", () => col.Find(Builders<IGadgetHier>.Filter.OfType<PhoneH>()).ToString());
        Report("QUERY OfType<PhoneH> result count", () => col.OfType<PhoneH>().Find(Builders<PhoneH>.Filter.Empty).ToList().Count);
    }

    // ----- Scenario 1 types: scalar interface, root-class concrete hierarchy -----
    public interface IGadgetScalar { ObjectId Id { get; set; } }

    [BsonKnownTypes(typeof(PhoneS), typeof(TabletS))]
    [BsonDiscriminator(RootClass = true)]
    public abstract class GadgetBaseS : IGadgetScalar { public ObjectId Id { get; set; } }
    public class PhoneS : GadgetBaseS { }
    public class TabletS : GadgetBaseS { }

    // ----- Scenario 2 types: hierarchical interface, root-class concrete hierarchy -----
    public interface IGadgetHier { ObjectId Id { get; set; } }

    [BsonKnownTypes(typeof(PhoneH), typeof(TabletH))]
    [BsonDiscriminator(RootClass = true)]
    public abstract class GadgetBaseH : IGadgetHier { public ObjectId Id { get; set; } }
    public class PhoneH : GadgetBaseH { }
    public class TabletH : GadgetBaseH { }
}
