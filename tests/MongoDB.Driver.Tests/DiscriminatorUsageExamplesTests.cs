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
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests;

// Educational examples (external API only): every situation where a discriminator is involved.
public class DiscriminatorUsageExamplesTests
{
    private readonly ITestOutputHelper _output;

    public DiscriminatorUsageExamplesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private IMongoDatabase GetDatabase() =>
        DriverTestConfiguration.Client.GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName);

    private void PrintStored(IMongoCollection<BsonDocument> raw, string label)
    {
        _output.WriteLine(label);
        foreach (var doc in raw.Find(Builders<BsonDocument>.Filter.Empty).ToList())
        {
            _output.WriteLine("  " + doc.ToJson());
        }
    }

    // ===== WRITE: when does _t get written? =====

    [Fact]
    public void Write_cases()
    {
        var db = GetDatabase();

        // (a) base collection + derived value -> _t written
        // (b) base collection + base value (scalar) -> NO _t
        var animals = db.GetCollection<Animal>("d_animals");
        animals.DeleteMany(Builders<Animal>.Filter.Empty);
        animals.InsertOne(new Dog { Name = "rex" });          // derived
        animals.InsertOne(new Animal { Name = "generic" });   // the base type itself
        PrintStored(animals.Database.GetCollection<BsonDocument>("d_animals"),
            "(a)/(b) base collection: derived gets _t, base value does NOT:");

        // (c) derived collection + derived value -> NO _t (nominal == actual)
        var dogs = db.GetCollection<Dog>("d_dogs");
        dogs.DeleteMany(Builders<Dog>.Filter.Empty);
        dogs.InsertOne(new Dog { Name = "fido" });
        PrintStored(dogs.Database.GetCollection<BsonDocument>("d_dogs"),
            "(c) collection typed as the derived type: no _t:");

        // (d) Required = true -> _t always written, even for the nominal type
        var reqs = db.GetCollection<RequiredBase>("d_required");
        reqs.DeleteMany(Builders<RequiredBase>.Filter.Empty);
        reqs.InsertOne(new RequiredBase { Name = "r" });
        PrintStored(reqs.Database.GetCollection<BsonDocument>("d_required"),
            "(d) [BsonDiscriminator(Required = true)]: _t even for the base/exact type:");

        // (e) polymorphic member + (f) polymorphic list element each get _t
        var garages = db.GetCollection<Garage>("d_garages");
        garages.DeleteMany(Builders<Garage>.Filter.Empty);
        garages.InsertOne(new Garage
        {
            Primary = new Dog { Name = "guard" },                       // member typed as base
            Animals = new List<Animal> { new Cat { Name = "mouser" } }  // list of base
        });
        PrintStored(garages.Database.GetCollection<BsonDocument>("d_garages"),
            "(e)/(f) polymorphic member and list element each get _t on the sub-document:");
    }

    // ===== READ: when is _t consulted? =====

    [Fact]
    public void Read_cases()
    {
        var db = GetDatabase();
        var raw = db.GetCollection<BsonDocument>("d_read");
        raw.DeleteMany(Builders<BsonDocument>.Filter.Empty);
        raw.InsertOne(new BsonDocument { { "_t", "Dog" }, { "Name", "rex" } });
        raw.InsertOne(new BsonDocument { { "_t", "Cat" }, { "Name", "tom" } });
        raw.InsertOne(new BsonDocument { { "Name", "nobody" } }); // no _t

        var animals = db.GetCollection<Animal>("d_read");
        var loaded = animals.Find(Builders<Animal>.Filter.Empty).ToList();
        _output.WriteLine("Reading a base-typed collection; _t decides the concrete CLR type:");
        foreach (var a in loaded)
        {
            _output.WriteLine($"  Name={a.Name} -> {a.GetType().Name}");
        }
    }

    // ===== QUERY: when is a _t filter added? =====

    [Fact]
    public void Query_cases()
    {
        var db = GetDatabase();
        var animals = db.GetCollection<Animal>("d_query");
        animals.DeleteMany(Builders<Animal>.Filter.Empty);
        animals.InsertOne(new Dog { Name = "rex" });
        animals.InsertOne(new Cat { Name = "tom" });
        animals.InsertOne(new Animal { Name = "generic" });

        _output.WriteLine("(1) Builders OfType:            " +
            animals.Find(Builders<Animal>.Filter.OfType<Dog>()).ToString());

        _output.WriteLine("(2) Collection OfType<Dog>():   " +
            animals.OfType<Dog>().Find(Builders<Dog>.Filter.Empty).ToString());

        _output.WriteLine("(3) plain Eq (no _t added):     " +
            animals.Find(Builders<Animal>.Filter.Eq(x => x.Name, "rex")).ToString());

        // (4) a collection typed directly as the derived type does NOT auto-add _t
        var dogsView = db.GetCollection<Dog>("d_query");
        _output.WriteLine("(4) GetCollection<Dog> Find():  " +
            dogsView.Find(Builders<Dog>.Filter.Empty).ToString() + "   <-- no _t filter!");

        // (5) LINQ
        try
        {
            _output.WriteLine("(5) LINQ OfType<Dog>():         " +
                animals.AsQueryable().OfType<Dog>().ToString());
            _output.WriteLine("(6) LINQ where x is Cat:        " +
                animals.AsQueryable().Where(x => x is Cat).ToString());
        }
        catch (Exception ex)
        {
            _output.WriteLine("LINQ ToString unavailable: " + ex.Message);
        }

        _output.WriteLine("results OfType<Dog> = " +
            animals.OfType<Dog>().Find(Builders<Dog>.Filter.Empty).ToList().Count + " (only dogs)");
    }

    // ----- Types -----

    [BsonKnownTypes(typeof(Dog), typeof(Cat))]
    public class Animal
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
    }

    public class Dog : Animal { }
    public class Cat : Animal { }

    [BsonDiscriminator(Required = true)]
    public class RequiredBase
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
    }

    public class Garage
    {
        public ObjectId Id { get; set; }
        public Animal Primary { get; set; }
        public List<Animal> Animals { get; set; }
    }
}
