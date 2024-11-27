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
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests.Jira
{
    public interface IFoo
    {
        ObjectId Id { get; set;}
        string SomeField { get; set;}
    }

    public class Widget1 : IFoo
    {
        public ObjectId Id {get;set;}
        public string SomeField { get; set; }
        public int Bar { get; set; }
    }

    public class Widget2 : IFoo
    {
        public ObjectId Id {get;set;}
        public string SomeField { get; set; }
        public Decimal Bar { get; set; }
    }

    public class CSharp1907Tests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public CSharp1907Tests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Type_discriminators_work_with_interfaces()
        {
            //TODO Why do we get an exception if we do not add this?
            var objectSerializer = new ObjectSerializer(type => true);
            BsonSerializer.RegisterSerializer(objectSerializer);

            var collectionNamespace = CoreTestConfiguration.GetCollectionNamespaceForTestClass(typeof(CSharp1907Tests));
            var databaseName = collectionNamespace.DatabaseNamespace.DatabaseName;

            var client = DriverTestConfiguration.Client;
            var database = client.GetDatabase(databaseName);
            var col = database.GetCollection<IFoo>("iFoo");

            // Clean up any existing documents
            await col.DeleteManyAsync(Builders<IFoo>.Filter.Empty);

            // Insert 2 new test documents of different types, each implementing IFoo
            await col.InsertOneAsync(new Widget1 { Id = ObjectId.GenerateNewId(), SomeField = "ABC", Bar = 10 });
            await col.InsertOneAsync(new Widget2 { Id = ObjectId.GenerateNewId(), SomeField = "ABC", Bar = 10M });

            _testOutputHelper.WriteLine("Documents in the database");
            var rawDocsInDatabase = await database.GetCollection<BsonDocument>("iFoo").Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();
            foreach(var doc in rawDocsInDatabase)
                _testOutputHelper.WriteLine(doc.ToJson());

            // Build a query using .OfType<> on the collection.
            var widget1QueryColOfType = col.OfType<Widget1>().Find(Builders<Widget1>.Filter.Eq(x => x.SomeField, "ABC"));
            var widget2QueryColOfType = col.OfType<Widget2>().Find(Builders<Widget2>.Filter.Eq(x => x.SomeField, "ABC"));
            _testOutputHelper.WriteLine("OfType<> on the Collection");
            _testOutputHelper.WriteLine(widget1QueryColOfType.ToString());
            _testOutputHelper.WriteLine(widget2QueryColOfType.ToString());
            _testOutputHelper.WriteLine("");

            // Build a query using .OfType<> on the Builder to change from IFoo to the concrete type.
            var widget1QueryBuilderOfType = col.Find(Builders<IFoo>.Filter.OfType<Widget1>(Builders<Widget1>.Filter.Eq(x => x.SomeField, "ABC")));
            var widget2QueryBuilderOfType = col.Find(Builders<IFoo>.Filter.OfType<Widget2>(Builders<Widget2>.Filter.Eq(x => x.SomeField, "ABC")));
            _testOutputHelper.WriteLine("OfType<> on the Builder");
            _testOutputHelper.WriteLine(widget1QueryBuilderOfType.ToString());
            _testOutputHelper.WriteLine(widget2QueryBuilderOfType.ToString());
            _testOutputHelper.WriteLine("");
        }
    }
}