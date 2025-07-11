﻿/* Copyright 2010-present MongoDB Inc.
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
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.TestHelpers.XunitExtensions;
using MongoDB.Driver.TestHelpers;
using Xunit;

namespace MongoDB.Driver.Tests.Linq.Linq3Implementation.Jira
{
    public class CSharp2727Tests : LinqIntegrationTest<CSharp2727Tests.ClassFixture>
    {
        public CSharp2727Tests(ClassFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void Find_with_predicate_on_Body_should_work()
        {
            RequireServer.Check().Supports(Feature.AggregateToString);
            var collection = Fixture.Collection;
            var filter = new ExpressionFilterDefinition<Entity>(x => new[] { "Test1", "Test2" }.Contains((string)x.Body["name"]));

            var serializerRegistry = BsonSerializer.SerializerRegistry;
            var documentSerializer = serializerRegistry.GetSerializer<Entity>();
            var renderedFilter = filter.Render(new(documentSerializer, serializerRegistry));

            renderedFilter.Should().Be("{ $expr : { $in : [{ $toString : '$Body.name' }, ['Test1', 'Test2']] } }");

            var cursor = collection.Find(filter);

            var results = cursor.ToList();
            results.Select(x => x.Id).Should().Equal(1, 2);
        }

        [Fact]
        public void Find_with_predicate_on_Caption_should_work()
        {
            var collection = Fixture.Collection;
            var filter = new ExpressionFilterDefinition<Entity>(x => new[] { "Test1", "Test2" }.Contains(x.Caption));

            var serializerRegistry = BsonSerializer.SerializerRegistry;
            var documentSerializer = serializerRegistry.GetSerializer<Entity>();
            var renderedFilter = filter.Render(new(documentSerializer, serializerRegistry));

            renderedFilter.Should().Be("{ Caption : { $in : ['Test1', 'Test2'] } }");

            var cursor = collection.FindSync(filter);

            var results = cursor.ToList();
            results.Select(x => x.Id).Should().Equal(1, 2);
        }

        [Fact]
        public void Where_with_predicate_on_Body_should_work()
        {
            RequireServer.Check().Supports(Feature.AggregateToString);
            var collection = Fixture.Collection;

            var queryable = collection
                .AsQueryable()
                .Where(x => new[] { "Test1", "Test2" }.Contains((string)x.Body["name"]));

            var stages = Translate(collection, queryable);
            AssertStages(stages, "{ $match : { $expr : { $in : [{ $toString : '$Body.name' }, ['Test1', 'Test2']] } } }");

            var results = queryable.ToList();
            results.Select(x => x.Id).Should().Equal(1, 2);
        }

        [Fact]
        public void Where_with_predicate_on_Caption_should_work()
        {
            var collection = Fixture.Collection;

            var queryable = collection
                .AsQueryable()
                .Where(x => new[] { "Test1", "Test2" }.Contains(x.Caption));

            var stages = Translate(collection, queryable);
            AssertStages(stages, "{ $match : { Caption : { $in : ['Test1', 'Test2'] } } }");

            var results = queryable.ToList();
            results.Select(x => x.Id).Should().Equal(1, 2);
        }

        public class Entity
        {
            public int Id { get; set; }
            public BsonDocument Body { get; set; }
            public string Caption { get; set; }
        }

        public sealed class ClassFixture : MongoCollectionFixture<Entity>
        {
            protected override IEnumerable<Entity> InitialData =>
            [
                new Entity { Id = 1, Body = BsonDocument.Parse("{ name : 'Test1' }"), Caption = "Test1" },
                new Entity { Id = 2, Body = BsonDocument.Parse("{ name : 'Test2' }"), Caption = "Test2" },
                new Entity { Id = 3, Body = BsonDocument.Parse("{ name : 'Test3' }"), Caption = "Test3" }
            ];
        }
    }
}
