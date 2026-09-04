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
using MongoDB.Driver.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests;

// Current (CSHARP-1907) end-to-end behavior when the collection document type is an interface.
public class InterfaceDiscriminatorExamplesTests
{
    private readonly ITestOutputHelper _output;

    public InterfaceDiscriminatorExamplesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private IMongoDatabase GetDatabase() =>
        DriverTestConfiguration.Client.GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName);

    private void Report(string label, Action action)
    {
        try
        {
            action();
            _output.WriteLine($"{label} => OK");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"{label} => THREW {ex.GetType().Name}: {ex.Message}");
        }
    }

    [Fact]
    public void Interface_collection_current_behavior()
    {
        var db = GetDatabase();
        var foos = db.GetCollection<IFoo>("d_ifoo");
        foos.DeleteMany(Builders<IFoo>.Filter.Empty);

        // WRITE
        foos.InsertOne(new Widget1 { Id = ObjectId.GenerateNewId(), SomeField = "ABC", Bar = 10 });
        foos.InsertOne(new Widget2 { Id = ObjectId.GenerateNewId(), SomeField = "ABC", Bar = 10m });

        var raw = db.GetCollection<BsonDocument>("d_ifoo");
        _output.WriteLine("WRITE: stored documents (note the bare _t):");
        foreach (var doc in raw.Find(Builders<BsonDocument>.Filter.Empty).ToList())
        {
            _output.WriteLine("  " + doc.ToJson());
        }

        // READ (plain Find, no OfType)
        _output.WriteLine("");
        _output.WriteLine("READ: plain Find on the interface collection resolves concrete types:");
        foreach (var foo in foos.Find(Builders<IFoo>.Filter.Empty).ToList())
        {
            _output.WriteLine($"  SomeField={foo.SomeField} -> {foo.GetType().Name}");
        }

        // QUERY: the four OfType / is surfaces
        _output.WriteLine("");
        _output.WriteLine("QUERY surfaces:");
        Report("  (1) Builders<IFoo>.Filter.OfType<Widget1>()",
            () => foos.Find(Builders<IFoo>.Filter.OfType<Widget1>()).ToString());
        Report("  (2) collection.OfType<Widget1>().Find(empty)",
            () => foos.OfType<Widget1>().Find(Builders<Widget1>.Filter.Empty).ToList());
        Report("  (3) LINQ AsQueryable().OfType<Widget1>()",
            () => foos.AsQueryable().OfType<Widget1>().ToList());
        Report("  (4) LINQ Where(x => x is Widget1)",
            () => foos.AsQueryable().Where(x => x is Widget1).ToList());
    }

    public interface IFoo
    {
        ObjectId Id { get; set; }
        string SomeField { get; set; }
    }

    public class Widget1 : IFoo
    {
        public ObjectId Id { get; set; }
        public string SomeField { get; set; }
        public int Bar { get; set; }
    }

    public class Widget2 : IFoo
    {
        public ObjectId Id { get; set; }
        public string SomeField { get; set; }
        public decimal Bar { get; set; }
    }
}
