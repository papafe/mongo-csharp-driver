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
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests;

// Faithfully simulates the proposed fix for interface hierarchies: a scalar convention whose
// "type and sub-types" enumeration narrows by IsAssignableFrom (the proposed Change 2). Verifies
// OfType to an INTERMEDIATE interface end-to-end against the live DB.
public class InterfaceHierarchyChange2ProbeTests
{
    static InterfaceHierarchyChange2ProbeTests()
    {
        var convention = new AssignableFromScalarConvention(typeof(Cat2), typeof(Dog2), typeof(Sparrow2));
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IAnimal2), convention);
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IMammal2), convention);
    }

    private readonly ITestOutputHelper _output;

    public InterfaceHierarchyChange2ProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private IMongoDatabase GetDatabase() =>
        DriverTestConfiguration.Client.GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName);

    [Fact]
    public void OfType_to_intermediate_interface_works_end_to_end()
    {
        var db = GetDatabase();
        var animals = db.GetCollection<IAnimal2>("d_hier_c2");
        animals.DeleteMany(Builders<IAnimal2>.Filter.Empty);
        animals.InsertOne(new Cat2 { Id = ObjectId.GenerateNewId() });
        animals.InsertOne(new Dog2 { Id = ObjectId.GenerateNewId() });
        animals.InsertOne(new Sparrow2 { Id = ObjectId.GenerateNewId() });

        _output.WriteLine("OfType<IMammal2>() filter: " +
            animals.Find(Builders<IAnimal2>.Filter.OfType<IMammal2>()).ToString());

        var mammals = animals.OfType<IMammal2>().Find(Builders<IMammal2>.Filter.Empty).ToList();
        _output.WriteLine("OfType<IMammal2>() results: " + string.Join(", ", mammals.Select(x => x.GetType().Name)));

        var cats = animals.OfType<Cat2>().Find(Builders<Cat2>.Filter.Empty).ToList();
        _output.WriteLine("OfType<Cat2>() results: " + string.Join(", ", cats.Select(x => x.GetType().Name)));

        mammals.Select(x => x.GetType().Name).OrderBy(x => x).Should().Equal("Cat2", "Dog2");
        cats.Select(x => x.GetType().Name).Should().Equal("Cat2");
    }

    public interface IAnimal2 { ObjectId Id { get; set; } }
    public interface IMammal2 : IAnimal2 { }
    public class Cat2 : IMammal2 { public ObjectId Id { get; set; } }
    public class Dog2 : IMammal2 { public ObjectId Id { get; set; } }
    public class Sparrow2 : IAnimal2 { public ObjectId Id { get; set; } }   // not a mammal

    // Mimics the proposed scalar-for-interfaces convention: enumerate known implementers and
    // narrow by IsAssignableFrom (this is exactly what proposed "Change 2" does in the registry walk).
    private sealed class AssignableFromScalarConvention : IScalarDiscriminatorConvention
    {
        private readonly Type[] _knownImplementers;

        public AssignableFromScalarConvention(params Type[] knownImplementers)
        {
            _knownImplementers = knownImplementers;
        }

        public string ElementName => "_t";

        public BsonValue GetDiscriminator(Type nominalType, Type actualType) =>
            actualType.IsAbstract || actualType.IsInterface ? null : actualType.Name;

        public BsonValue[] GetDiscriminatorsForTypeAndSubTypes(Type type) =>
            _knownImplementers
                .Where(t => type.IsAssignableFrom(t))
                .Select(t => (BsonValue)t.Name)
                .OrderBy(x => x)
                .ToArray();

        public Type GetActualType(IBsonReader bsonReader, Type nominalType)
        {
            var value = ReadDiscriminator(bsonReader);
            return _knownImplementers.FirstOrDefault(t => t.Name == value)
                ?? throw new Exception($"Unknown discriminator: {value}");
        }

        private string ReadDiscriminator(IBsonReader bsonReader)
        {
            string value = null;
            if (bsonReader.GetCurrentBsonType() == BsonType.Document)
            {
                var bookmark = bsonReader.GetBookmark();
                bsonReader.ReadStartDocument();
                if (bsonReader.FindElement("_t"))
                {
                    var context = BsonDeserializationContext.CreateRoot(bsonReader);
                    if (BsonValueSerializer.Instance.Deserialize(context) is BsonString s)
                    {
                        value = s.Value;
                    }
                }
                bsonReader.ReturnToBookmark(bookmark);
            }
            return value;
        }
    }
}
