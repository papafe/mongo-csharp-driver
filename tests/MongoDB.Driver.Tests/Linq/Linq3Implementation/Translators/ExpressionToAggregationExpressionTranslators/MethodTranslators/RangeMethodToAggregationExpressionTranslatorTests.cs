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

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Driver.TestHelpers;
using Xunit;

namespace MongoDB.Driver.Tests.Linq.Linq3Implementation.Translators.ExpressionToAggregationExpressionTranslators.MethodTranslators
{
    public class RangeMethodToAggregationExpressionTranslatorTests : LinqIntegrationTest<RangeMethodToAggregationExpressionTranslatorTests.ClassFixture>
    {
        public RangeMethodToAggregationExpressionTranslatorTests(ClassFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void Range_should_work()
        {
            var collection = Fixture.Collection;

            var queryable = collection.AsQueryable().Select(x => Enumerable.Range(x.Start, x.Count));

            var stages = Translate(collection, queryable);
            AssertStages(stages, "{ $project : { _v : { $range : ['$Start', { $add : ['$Start', '$Count'] }] }, _id : 0 } }");

            var results = queryable.ToList();
            results.Should().HaveCount(2);
            results[0].Should().Equal(1, 2);
            results[1].Should().Equal(3, 4, 5, 6);
        }

        public class C
        {
            public int Id { get; set; }
            public int Start { get; set; }
            public int Count { get; set; }
        }

        public sealed class ClassFixture : MongoCollectionFixture<C>
        {
            protected override IEnumerable<C> InitialData =>
            [
                new C { Id = 1, Start = 1, Count = 2 },
                new C { Id = 2, Start = 3, Count = 4 }
            ];
        }
    }
}
