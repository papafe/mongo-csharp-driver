﻿/* Copyright 2017-present MongoDB Inc.
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
using MongoDB.Driver.Linq.Linq3Implementation;
using MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToExecutableQueryTranslators;
using Xunit;

namespace MongoDB.Driver.Tests.Linq.Linq3ImplementationWithLinq2Tests
{
    [Trait("Category", "Integration")]
    public class MongoQueryableWithDotNotationTests
    {
        [Fact]
        public void Where_with_ExtraInfo_Type_and_ExtraInfo_NotNullableType_should_render_correctly()
        {
            var client = GetClient();
            var subject = CreateSubject(client);

            var result = subject.Where(c => c.ExtraInfo.Type == 1 && c.ExtraInfo.NotNullableType == 1);

            AssertStage(result, "{ \"$match\" : { \"ExtraInfo.Type\" : 1, \"ExtraInfo.NotNullableType\" : 1 } }");
        }

        [Fact]
        public void Where_with_ExtraInfo_Type_should_render_correctly()
        {
            var client = GetClient();
            var subject = CreateSubject(client);

            var result = subject.Where(c => c.ExtraInfo.Type == null);

            AssertStage(result, "{ \"$match\" : { \"ExtraInfo.Type\" : null } }");
        }

        [Fact]
        public void Where_with_ExtraInfo_Type_with_Value_should_render_correctly()
        {
            var client = GetClient();
            var subject = CreateSubject(client);

            var result = subject.Where(c => c.ExtraInfo.Type.Value == 2);

            AssertStage(result, "{ \"$match\" : { \"ExtraInfo.Type\" : 2 } }");
        }

        [Fact]
        public void Where_with_ExtraInfo_Type_with_Value_and_nullable_variable_should_render_correctly()
        {
            var client = GetClient();
            var subject = CreateSubject(client);
            int? infoType = 3;

            var result = subject.Where(c => c.ExtraInfo.Type.Value == infoType);

            AssertStage(result, "{ \"$match\" : { \"ExtraInfo.Type\" : 3 } }");
        }

        [Fact]
        public void Where_with_Contains_should_render_correctly()
        {
            var client = GetClient();
            var subject = CreateSubject(client);
            var list = new List<int> { 4, 5 };

            var result = subject.Where(c => list.Contains(c.ExtraInfo.Type.Value) || list.Contains(c.ExtraInfo.NotNullableType));

            AssertStage(result, "{ \"$match\" : { \"$or\" : [{ \"ExtraInfo.Type\" : { \"$in\" : [4, 5] } }, { \"ExtraInfo.NotNullableType\" : { \"$in\" : [4, 5] } }] } }");
        }

        // private methods
        private void AssertStage<T>(IQueryable<T> queryable, string expectedStage)
        {
            var stages = Translate(queryable);
            stages.Should().HaveCount(1);
            stages[0].Should().Be(expectedStage);
        }

        private IQueryable<Car> CreateSubject(IMongoClient client)
        {
            var database = client.GetDatabase("test");
            var collection = database.GetCollection<Car>("test");
            return collection.AsQueryable();
        }

        private IMongoClient GetClient()
        {
            return DriverTestConfiguration.Client;
        }

        private BsonDocument[] Translate<T>(IQueryable<T> queryable)
        {
            var provider = (MongoQueryProvider<T>)queryable.Provider;
            var executableQuery = ExpressionToExecutableQueryTranslator.Translate<T, T>(provider, queryable.Expression, translationOptions: null);
            return executableQuery.Pipeline.Ast.Stages.Select(s => (BsonDocument)s.Render()).ToArray();
        }

        // nested types
        private class Car
        {
            public Guid Id { get; set; }
            public ExtraInfo ExtraInfo { get; set; }
        }

        private class ExtraInfo
        {
            public int NotNullableType { get; set; }
            public int? Type { get; set; }
        }
    }
}
