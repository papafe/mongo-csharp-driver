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

using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests;

// Educational examples (external API only) showing how discriminator conventions behave today,
// to ground the CSHARP-1907 design discussion. Not intended as permanent regression tests.
public class DiscriminatorConventionExamplesTests
{
    private readonly ITestOutputHelper _output;

    public DiscriminatorConventionExamplesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private IMongoDatabase GetDatabase() =>
        DriverTestConfiguration.Client.GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName);

    private void PrintStored<T>(IMongoCollection<T> typed)
    {
        var raw = typed.Database.GetCollection<BsonDocument>(typed.CollectionNamespace.CollectionName);
        foreach (var doc in raw.Find(Builders<BsonDocument>.Filter.Empty).ToList())
        {
            _output.WriteLine("  stored: " + doc.ToJson());
        }
    }

    // ---------- 1. Scalar discriminator (the default for ordinary class hierarchies) ----------

    [Fact]
    public void Scalar_default_hierarchy()
    {
        var db = GetDatabase();
        var vehicles = db.GetCollection<Vehicle>("disc_vehicles");
        vehicles.DeleteMany(Builders<Vehicle>.Filter.Empty);

        vehicles.InsertOne(new Car { Name = "c1" });
        vehicles.InsertOne(new Truck { Name = "t1" });

        _output.WriteLine("== Scalar (default) ==");
        _output.WriteLine("WRITE: what ends up in the database");
        PrintStored(vehicles);

        _output.WriteLine("QUERY: Builders<Vehicle>.Filter.OfType<Car>()");
        _output.WriteLine("  " + vehicles.Find(Builders<Vehicle>.Filter.OfType<Car>()).ToString());

        _output.WriteLine("QUERY results OfType<Car>: " +
            string.Join(", ", vehicles.OfType<Car>().Find(Builders<Car>.Filter.Empty).ToList().Select(x => x.Name)));
    }

    // ---------- 2. Hierarchical discriminator (opt-in via a root class) ----------

    [Fact]
    public void Hierarchical_via_root_class()
    {
        var db = GetDatabase();
        var employees = db.GetCollection<Employee>("disc_employees");
        employees.DeleteMany(Builders<Employee>.Filter.Empty);

        employees.InsertOne(new Manager { Name = "m1" });
        employees.InsertOne(new Engineer { Name = "e1" });

        _output.WriteLine("== Hierarchical (RootClass = true) ==");
        _output.WriteLine("WRITE: what ends up in the database");
        PrintStored(employees);

        _output.WriteLine("QUERY: Builders<Employee>.Filter.OfType<Manager>()");
        _output.WriteLine("  " + employees.Find(Builders<Employee>.Filter.OfType<Manager>()).ToString());

        _output.WriteLine("QUERY results OfType<Manager>: " +
            string.Join(", ", employees.OfType<Manager>().Find(Builders<Manager>.Filter.Empty).ToList().Select(x => x.Name)));
    }

    // ---------- 3. Abstract base + abstract intermediate (scalar) ----------

    [Fact]
    public void Abstract_base_and_intermediate()
    {
        var db = GetDatabase();
        var shapes = db.GetCollection<Shape>("disc_shapes");
        shapes.DeleteMany(Builders<Shape>.Filter.Empty);

        shapes.InsertOne(new Circle());
        shapes.InsertOne(new Square());
        shapes.InsertOne(new LongRectangle());

        _output.WriteLine("== Abstract base (Shape) + abstract intermediate (Rectangle) ==");
        _output.WriteLine("WRITE: what ends up in the database (no instance of the abstract types)");
        PrintStored(shapes);

        _output.WriteLine("QUERY: OfType<Square>()  (a concrete leaf)");
        _output.WriteLine("  " + shapes.Find(Builders<Shape>.Filter.OfType<Square>()).ToString());

        _output.WriteLine("QUERY: OfType<Rectangle>()  (an abstract intermediate -> all concrete subtypes)");
        _output.WriteLine("  " + shapes.Find(Builders<Shape>.Filter.OfType<Rectangle>()).ToString());

        _output.WriteLine("QUERY results OfType<Rectangle>: " +
            shapes.OfType<Rectangle>().Find(Builders<Rectangle>.Filter.Empty).ToList().Count + " docs");
    }

    // ----- Types -----

    // 1. Scalar default
    [BsonKnownTypes(typeof(Car), typeof(Truck))]
    public class Vehicle
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
    }

    public class Car : Vehicle { }
    public class Truck : Vehicle { }

    // 2. Hierarchical via root class
    [BsonKnownTypes(typeof(Manager), typeof(Engineer))]
    [BsonDiscriminator(RootClass = true)]
    public class Employee
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
    }

    public class Manager : Employee { }
    public class Engineer : Employee { }

    // 3. Abstract base + abstract intermediate
    [BsonKnownTypes(typeof(Circle), typeof(Rectangle))]
    public abstract class Shape
    {
        public ObjectId Id { get; set; }
    }

    public class Circle : Shape { }

    [BsonKnownTypes(typeof(Square), typeof(LongRectangle))]
    public abstract class Rectangle : Shape { }

    public class Square : Rectangle { }
    public class LongRectangle : Rectangle { }
}
