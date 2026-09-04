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
using Xunit;

namespace MongoDB.Driver.Tests;

// Verification harness for CSHARP-1907 (type discriminators when the collection document type is an interface).
// The "current behavior" facts document today's (buggy) state and are expected to change once the ticket is fixed.
// The "fix direction" facts prototype option A (give interfaces a scalar-style convention) without touching production code.
public class InterfaceDiscriminatorTests : LinqIntegrationTest<InterfaceDiscriminatorTests.ClassFixture>
{
    static InterfaceDiscriminatorTests()
    {
        // Prototype the fix: register a working scalar discriminator convention for the interface hierarchy.
        BsonSerializer.RegisterDiscriminatorConvention(typeof(IShape), new ShapeDiscriminatorConvention());
    }

    public InterfaceDiscriminatorTests(ClassFixture fixture)
        : base(fixture)
    {
    }

    // ----- Findings: current behavior with the default (Object) convention -----

    [Fact]
    public void Default_interface_resolves_to_ObjectDiscriminatorConvention()
    {
        var convention = BsonSerializer.LookupDiscriminatorConvention(typeof(IPet));
        convention.Should().BeOfType<ObjectDiscriminatorConvention>();

        // a concrete class, by contrast, defaults to the scalar convention
        BsonSerializer.LookupDiscriminatorConvention(typeof(Cat)).Should().BeOfType<ScalarDiscriminatorConvention>();
    }

    [Fact]
    public void Default_interface_OfType_filter_currently_throws()
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<IPet>();
        var renderArgs = new RenderArgs<IPet>(serializer, BsonSerializer.SerializerRegistry);

        var exception = Record.Exception(() => Builders<IPet>.Filter.OfType<Cat>().Render(renderArgs));

        exception.Should().BeOfType<NotSupportedException>();
        exception.Message.Should().Be("OfType is not supported with the configured discriminator convention.");
    }

    [Fact]
    public void Default_interface_write_produces_bare_discriminator()
    {
        // Writing through the interface produces a BARE discriminator (delegated to the concrete class map),
        // even though the Object convention's GetDiscriminator would produce an assembly-qualified value.
        var json = new Cat { Id = 1 }.ToJson<IPet>();
        json.Should().Contain("\"_t\" : \"Cat\"");

        var objectConventionDiscriminator = ObjectDiscriminatorConvention.Instance.GetDiscriminator(typeof(IPet), typeof(Cat));
        objectConventionDiscriminator.AsString.Should().StartWith("MongoDB.Driver.Tests.InterfaceDiscriminatorTests+Cat, ");
    }

    // ----- Finding: why the registry walk misses interface implementers -----

    [Fact]
    public void Interface_is_not_a_subclass_of_its_implementers()
    {
        // BsonSerializer.GetDiscriminatorsForTypeAndSubTypes uses t.IsSubclassOf(type), which is class-only.
        typeof(Cat).IsSubclassOf(typeof(IPet)).Should().BeFalse();
        // The relationship that WOULD match interface implementers:
        typeof(IPet).IsAssignableFrom(typeof(Cat)).Should().BeTrue();
    }

    // ----- Fix direction (option A): a scalar convention on the interface makes OfType work -----

    [Fact]
    public void Scalar_interface_resolves_to_registered_scalar_convention()
    {
        BsonSerializer.LookupDiscriminatorConvention(typeof(IShape)).Should().BeOfType<ShapeDiscriminatorConvention>();
    }

    [Fact]
    public void Scalar_interface_OfType_concrete_renders_eq()
    {
        var renderArgs = new RenderArgs<IShape>(Fixture.ShapeCollection.DocumentSerializer, BsonSerializer.SerializerRegistry);

        var rendered = Builders<IShape>.Filter.OfType<Square>().Render(renderArgs);

        rendered.Should().Be("{ _t : 'Square' }");
    }

    [Fact]
    public void Scalar_interface_OfType_abstract_renders_in()
    {
        var renderArgs = new RenderArgs<IShape>(Fixture.ShapeCollection.DocumentSerializer, BsonSerializer.SerializerRegistry);

        var rendered = Builders<IShape>.Filter.OfType<Polygon>().Render(renderArgs);

        rendered.Should().Be("{ _t : { $in : ['Square', 'Triangle'] } }");
    }

    [Fact]
    public void Scalar_interface_OfType_round_trips_against_database()
    {
        var collection = Fixture.ShapeCollection;

        var squares = collection.FindSync(Builders<IShape>.Filter.OfType<Square>()).ToList();
        squares.Select(x => x.Id).Should().Equal(1);

        var polygons = collection.FindSync(Builders<IShape>.Filter.OfType<Polygon>()).ToList();
        polygons.Select(x => x.Id).Should().Equal(1, 2);
    }

    // ----- Test types -----

    // Default regime (no custom convention -> Object convention)
    public interface IPet
    {
        int Id { get; set; }
    }

    public class Cat : IPet
    {
        public int Id { get; set; }
    }

    public class Dog : IPet
    {
        public int Id { get; set; }
    }

    // Scalar regime (custom scalar convention registered for IShape)
    public interface IShape
    {
        int Id { get; set; }
    }

    public abstract class Polygon : IShape
    {
        public int Id { get; set; }
    }

    public class Square : Polygon
    {
    }

    public class Triangle : Polygon
    {
    }

    public class Circle : IShape
    {
        public int Id { get; set; }
    }

    // A scalar discriminator convention that understands the IShape interface hierarchy,
    // including interface implementers (which the built-in registry walk cannot enumerate).
    public class ShapeDiscriminatorConvention : IScalarDiscriminatorConvention
    {
        public string ElementName => "_t";

        public Type GetActualType(IBsonReader bsonReader, Type nominalType)
        {
            var discriminatorValue = ReadDiscriminatorValue(bsonReader);
            return discriminatorValue switch
            {
                "Square" => typeof(Square),
                "Triangle" => typeof(Triangle),
                "Circle" => typeof(Circle),
                _ => throw new Exception($"Invalid discriminator value: {discriminatorValue}.")
            };
        }

        public BsonValue GetDiscriminator(Type nominalType, Type actualType)
            => actualType.IsAbstract || actualType.IsInterface ? null : actualType.Name;

        public BsonValue[] GetDiscriminatorsForTypeAndSubTypes(Type type)
            => type.Name switch
            {
                "IShape" => ["Circle", "Square", "Triangle"],
                "Polygon" => ["Square", "Triangle"],
                "Square" => ["Square"],
                "Triangle" => ["Triangle"],
                "Circle" => ["Circle"],
                _ => throw new ArgumentException($"Invalid type: {type.Name}.")
            };

        private string ReadDiscriminatorValue(IBsonReader bsonReader)
        {
            string discriminatorValue = null;

            if (bsonReader.GetCurrentBsonType() == BsonType.Document)
            {
                var bookmark = bsonReader.GetBookmark();
                bsonReader.ReadStartDocument();
                if (bsonReader.FindElement("_t"))
                {
                    var context = BsonDeserializationContext.CreateRoot(bsonReader);
                    if (BsonValueSerializer.Instance.Deserialize(context) is BsonString bsonString)
                    {
                        discriminatorValue = bsonString.Value;
                    }
                }
                bsonReader.ReturnToBookmark(bookmark);
            }

            return discriminatorValue;
        }
    }

    public sealed class ClassFixture : MongoDatabaseFixture
    {
        public IMongoCollection<IShape> ShapeCollection { get; private set; }

        protected override void InitializeFixture()
        {
            ShapeCollection = CreateCollection<IShape>("shapes");
            ShapeCollection.InsertMany(
            [
                new Square { Id = 1 },
                new Triangle { Id = 2 },
                new Circle { Id = 3 }
            ]);
        }
    }
}
