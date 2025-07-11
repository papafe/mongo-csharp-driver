﻿/* Copyright 2019-present MongoDB Inc.
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
using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.TestHelpers;
using MongoDB.Bson.TestHelpers.JsonDrivenTests;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Clusters.ServerSelectors;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.Servers;
using MongoDB.Driver.Core.TestHelpers;
using MongoDB.Driver.Core.TestHelpers.JsonDrivenTests;
using MongoDB.Driver.Core.TestHelpers.Logging;
using MongoDB.Driver.Core.TestHelpers.XunitExtensions;
using MongoDB.Driver.Tests.JsonDrivenTests;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MongoDB.Driver.Tests.Specifications.Runner
{
    public abstract class MongoClientJsonDrivenTestRunnerBase : LoggableTestClass
    {
        // private fields
        private bool _async;
        private readonly string _collectionNameKey = "collection_name";
        private readonly string _databaseNameKey = "database_name";
        private readonly string _dataKey = "data";
        private readonly string _expectationsKey = "expectations";
        private readonly string _failPointKey = "failPoint";
        private readonly string _operationsKey = "operations";
        private readonly string _outcomeKey = "outcome";
        private readonly bool _shouldEventsBeChecked = true;
        private readonly string _skipReasonKey = "skipReason";
        private readonly HashSet<string> _defaultCommandsToNotCapture = new HashSet<string>
        {
            "hello",
            OppressiveLanguageConstants.LegacyHelloCommandName,
            "getLastError",
            "authenticate",
            "saslStart",
            "saslContinue",
            "getnonce"
        };

        private readonly string[] _expectedSharedColumns = { "_path", "runOn", "database_name", "collection_name", "data", "tests" };

        private string DatabaseName { get; set; }
        private string CollectionName { get; set; }

        private IDictionary<string, object> _objectMap = null;

        private protected IServer _failPointServer = null;

        protected BsonDocument LastKnownClusterTime { get; set; }

        // public constructors
        public MongoClientJsonDrivenTestRunnerBase(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        // Protected
        // Virtual properties
        protected virtual HashSet<string> DefaultCommandsToNotCapture => _defaultCommandsToNotCapture;

        protected virtual string[] ExpectedSharedColumns => _expectedSharedColumns;

        protected virtual string DatabaseNameKey => _databaseNameKey;

        protected virtual string CollectionNameKey => _collectionNameKey;

        protected virtual string ExpectationsKey => _expectationsKey;

        protected abstract string[] ExpectedTestColumns { get; }

        protected virtual string OutcomeKey => _outcomeKey;

        protected virtual string FailPointKey => _failPointKey;

        protected virtual string DataKey => _dataKey;

        protected virtual string OperationsKey => _operationsKey;

        protected virtual string SkipReasonKey => _skipReasonKey;

        protected virtual bool ShouldEventsBeChecked => _shouldEventsBeChecked;

        protected IDictionary<string, object> ObjectMap => _objectMap;

        // Virtual methods
        protected virtual void AssertEvent(object actualEvent, BsonDocument expectedEvent)
        {
            AssertEvent(actualEvent, expectedEvent, null);
        }

        protected virtual void AssertEvent(object actualEvent, BsonDocument expectedEvent, Action<object, BsonDocument> prepareEventResult, Func<KeyValuePair<string, BsonValue>[]> getPlaceholders = null)
        {
            if (expectedEvent.ElementCount != 1)
            {
                throw new FormatException("Expected event must be a document with a single element with a name the specifies the type of the event.");
            }

            prepareEventResult?.Invoke(actualEvent, expectedEvent);

            var eventType = expectedEvent.GetElement(0).Name;
            var eventAsserter = EventAsserterFactory.CreateAsserter(eventType);
            if (getPlaceholders != null)
            {
                eventAsserter.ConfigurePlaceholders(getPlaceholders());
            }

            eventAsserter.AssertAspects(actualEvent, expectedEvent[0].AsBsonDocument);
        }

        protected virtual void AssertEvents(EventCapturer eventCapturer, BsonDocument test)
        {
            if (test.Contains(ExpectationsKey))
            {
                var expectedEvents = test[ExpectationsKey].AsBsonArray.Cast<BsonDocument>().ToList();
                var actualEvents = ExtractEventsForAsserting(eventCapturer);

                var n = Math.Min(actualEvents.Count, expectedEvents.Count);
                for (var index = 0; index < n; index++)
                {
                    var actualEvent = actualEvents[index];
                    var expectedEvent = expectedEvents[index];
                    AssertEvent(actualEvent, expectedEvent);
                }
                if (actualEvents.Count < expectedEvents.Count)
                {
                    throw new Exception($"Missing command started event: {expectedEvents[n]}.");
                }

                if (actualEvents.Count > expectedEvents.Count)
                {
                    throw new Exception($"Unexpected command started event: {GetEventName(actualEvents[n])}.");
                }
            }

            string GetEventName(object @event)
            {
                return @event is CommandStartedEvent commandStartedEvent ? commandStartedEvent.CommandName : @event.GetType().Name;
            }
        }

        protected virtual void AssertOperation(string name, JsonDrivenTest jsonDrivenTest)
        {
            jsonDrivenTest.Assert();
        }

        protected virtual void AssertOutcome(BsonDocument test)
        {
            if (test.TryGetValue(OutcomeKey, out var outcome))
            {
                foreach (var aspect in outcome.AsBsonDocument)
                {
                    switch (aspect.Name)
                    {
                        case "collection":
                            VerifyCollectionOutcome(aspect.Value.AsBsonDocument);
                            break;

                        default:
                            throw new FormatException($"Unexpected outcome aspect: {aspect.Name}.");
                    }
                }
            }
        }

        protected virtual void CheckServerRequirements(BsonDocument document)
        {
            Logger.LogDebug("Checking server requirements");

            if (document.TryGetValue("runOn", out var runOn))
            {
                RequireServer.Check().RunOn(runOn.AsBsonArray);
            }

            Logger.LogDebug("Checked server requirements");
        }

        protected virtual void ConfigureClientSettings(MongoClientSettings settings, BsonDocument test)
        {
            if (test.Contains("clientOptions"))
            {
                foreach (var option in test["clientOptions"].AsBsonDocument)
                {
                    if (!TryConfigureClientOption(settings, option))
                    {
                        throw new FormatException($"Unexpected client option: \"{option.Name}\".");
                    }
                }
            }
        }

        protected virtual void CreateCollection(IMongoClient client, string databaseName, string collectionName, BsonDocument test, BsonDocument shared)
        {
            Logger.LogDebug("Creating collection {0} in {1} db", databaseName, collectionName);

            var session = client.StartSession();
            var database = client.GetDatabase(databaseName).WithWriteConcern(WriteConcern.WMajority);
            database.CreateCollection(session, collectionName);

            LastKnownClusterTime = session.ClusterTime;
        }

        protected virtual IMongoClient CreateClientForTestSetup()
        {
            return DriverTestConfiguration.Client;
        }

        protected virtual void CustomDataValidation(BsonDocument shared, BsonDocument test)
        {
            // do nothing by default.
        }

        private protected virtual JsonDrivenTestFactory CreateJsonDrivenTestFactory(IMongoClient mongoClient, string databaseName, string collectionName, Dictionary<string, object> objectMap, EventCapturer eventCapturer)
        {
            return new JsonDrivenTestFactory(mongoClient, databaseName, collectionName, bucketName: null, objectMap, eventCapturer);
        }

        protected virtual void DropCollection(IMongoClient client, string databaseName, string collectionName, BsonDocument test, BsonDocument shared)
        {
            Logger.LogDebug("Dropping collection {0} in {1} db", databaseName, collectionName);

            var database = client.GetDatabase(databaseName).WithWriteConcern(WriteConcern.WMajority);
            database.DropCollection(collectionName);
        }

        protected virtual void ExecuteOperations(IMongoClient client, Dictionary<string, object> objectMap, BsonDocument test, EventCapturer eventCapturer = null)
        {
            _objectMap = objectMap;

            var factory = CreateJsonDrivenTestFactory(client, DatabaseName, CollectionName, objectMap, eventCapturer);

            foreach (var operation in test[OperationsKey].AsBsonArray.Cast<BsonDocument>())
            {
                Logger.LogDebug("Executing operation {0}", operation["name"].AsString);

                ModifyOperationIfNeeded(operation);
                var receiver = operation["object"].AsString;
                var name = operation["name"].AsString;
                var jsonDrivenTest = factory.CreateTest(receiver, name);
                jsonDrivenTest.Arrange(operation);
                if (test["async"].AsBoolean)
                {
                    jsonDrivenTest.ActAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
                else
                {
                    jsonDrivenTest.Act(CancellationToken.None);
                }
                AssertOperation(name, jsonDrivenTest);
            }
        }

        protected virtual string GetCollectionName(BsonDocument definition)
        {
            return definition[CollectionNameKey].AsString;
        }

        protected virtual string GetDatabaseName(BsonDocument definition)
        {
            return definition[DatabaseNameKey].AsString;
        }

        protected virtual void InsertData(IMongoClient client, string databaseName, string collectionName, BsonDocument shared)
        {
            Logger.LogDebug("Inserting data to {0} in {1} db", databaseName, collectionName);

            if (shared.Contains(DataKey))
            {
                BsonDocument lastServerTime = null;
                var documents = shared[DataKey].AsBsonArray.Cast<BsonDocument>().ToList();

                if (documents.Count > 0)
                {
                    var session = client.StartSession();

                    var database = client.GetDatabase(databaseName);
                    var collection = database.GetCollection<BsonDocument>(collectionName).WithWriteConcern(WriteConcern.WMajority);
                    collection.InsertMany(session, documents);

                    lastServerTime = session.ClusterTime;
                }

                LastKnownClusterTime = lastServerTime ?? LastKnownClusterTime;
            }
        }

        protected virtual void ModifyOperationIfNeeded(BsonDocument operation)
        {
            // do nothing by default
        }

        protected virtual void RunTest(BsonDocument shared, BsonDocument test, EventCapturer eventCapturer)
        {
            Logger.LogDebug("Running test");

            using (var client = CreateMongoClient(test, eventCapturer))
            {
                Logger.LogDebug("Disposable client created with cluster:{0}", client.Cluster.ClusterId);

                ExecuteOperations(client, objectMap: null, test, eventCapturer);
            }
        }

        protected virtual void TestInitialize(IMongoClient client, BsonDocument test, BsonDocument shared)
        {
            // do nothing by default.
        }

        protected virtual bool TryConfigureClientOption(MongoClientSettings settings, BsonElement option)
        {
            switch (option.Name)
            {
                case "readConcernLevel":
                    var level = (ReadConcernLevel)Enum.Parse(typeof(ReadConcernLevel), option.Value.AsString, ignoreCase: true);
                    settings.ReadConcern = new ReadConcern(level);
                    break;

                case "readPreference":
                    settings.ReadPreference = ReadPreferenceFromBsonValue(option.Value);
                    break;

                case "retryWrites":
                    settings.RetryWrites = option.Value.ToBoolean();
                    break;

                case "w":
                    if (option.Value.IsString)
                    {
                        settings.WriteConcern = new WriteConcern(option.Value.AsString);
                    }
                    else
                    {
                        settings.WriteConcern = new WriteConcern(option.Value.ToInt32());
                    }
                    break;

                case "retryReads":
                    settings.RetryReads = option.Value.ToBoolean();
                    break;

                case "appname":
                    settings.ApplicationName = FailPoint.DecorateApplicationName($"{option.Value}", _async);
                    break;

                case "connectTimeoutMS":
                    var connectTimeoutMS = option.Value.ToInt32();
                    settings.ConnectTimeout = connectTimeoutMS == 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(connectTimeoutMS);
                    break;

                case "directConnection":
                    var DirectConnection = option.Value.ToBoolean();
                    settings.DirectConnection = DirectConnection;

                    if (DirectConnection)
                    {
                        settings.Servers = [settings.Servers.First()];
                    }
                    break;

                case "heartbeatFrequencyMS":
                    settings.HeartbeatInterval = TimeSpan.FromMilliseconds(option.Value.ToInt32());
                    break;

                case "maxPoolSize":
                    settings.MaxConnectionPoolSize = option.Value.ToInt32();
                    break;

                case "minPoolSize":
                    settings.MinConnectionPoolSize = option.Value.ToInt32();
                    break;

                case "serverSelectionTimeoutMS":
                    settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(option.Value.ToInt32());
                    break;

                case "socketTimeoutMS":
                    settings.SocketTimeout = TimeSpan.FromMilliseconds(option.Value.ToInt32());
                    break;

                default:
                    return false;
            }

            return true;
        }

        protected virtual void VerifyCollectionData(IEnumerable<BsonDocument> expectedDocuments)
        {
            VerifyCollectionData(expectedDocuments, DatabaseName, CollectionName);
        }

        protected virtual void VerifyCollectionData(IEnumerable<BsonDocument> expectedDocuments, string databaseName, string collectionName)
        {
            VerifyCollectionData(expectedDocuments, databaseName, collectionName, prepareExpectedResult: null);
        }

        protected virtual void VerifyCollectionOutcome(BsonDocument outcome)
        {
            var collectionName = CollectionName;

            foreach (var aspect in outcome)
            {
                switch (aspect.Name)
                {
                    case "name":
                        collectionName = aspect.Value.AsString;
                        break;

                    case "data":
                        VerifyCollectionData(aspect.Value.AsBsonArray.Cast<BsonDocument>(), DatabaseName, collectionName);
                        break;

                    default:
                        throw new FormatException($"Unexpected collection outcome aspect: {aspect.Name}.");
                }
            }
        }

        private protected FailPoint ConfigureFailPoint(BsonDocument test, IMongoClient client)
        {
            if (test.TryGetValue(FailPointKey, out var failPoint))
            {
                Logger.LogDebug("Configuring failpoint");

                var cluster = client.GetClusterInternal();

                var settings = client.Settings.Clone();
                ConfigureClientSettings(settings, test);

                if (settings.DirectConnection == true)
                {
                    var serverAddress = EndPointHelper.Parse(settings.Server.ToString());

                    var selector = new EndPointServerSelector(serverAddress);
                    _failPointServer = cluster.SelectServer(OperationContext.NoTimeout, selector);
                }
                else
                {
                    _failPointServer = cluster.SelectServer(OperationContext.NoTimeout, WritableServerSelector.Instance);
                }

                var session = NoCoreSession.NewHandle();
                var command = failPoint.AsBsonDocument;

                return FailPoint.Configure(_failPointServer, session, command, _async);
            }

            return null;
        }

        protected IMongoClient CreateMongoClient(BsonDocument test, EventCapturer eventCapturer)
        {
            var useMultipleShardRouters = test.GetValue("useMultipleMongoses", false).AsBoolean;
            RequireServer.Check().MultipleMongosesIfSharded(required: useMultipleShardRouters);

            return DriverTestConfiguration.CreateMongoClient(
                settings =>
                {
                    settings.HeartbeatInterval = TimeSpan.FromMilliseconds(5); // the default value for spec tests
                    ConfigureClientSettings(settings, test);
                    if (eventCapturer != null)
                    {
                        settings.ClusterConfigurator = c => c.Subscribe(eventCapturer);
                    }
                    settings.LoggingSettings = LoggingSettings;
                },
                useMultipleShardRouters);
        }

        protected virtual List<object> ExtractEventsForAsserting(EventCapturer eventCapturer)
        {
            var events = new List<object>();

            while (eventCapturer.Any())
            {
                events.Add(eventCapturer.Next());
            }

            return events;
        }

        protected virtual EventCapturer InitializeEventCapturer(EventCapturer eventCapturer)
        {
            return eventCapturer.Capture<CommandStartedEvent>(e => !DefaultCommandsToNotCapture.Contains(e.CommandName));
        }

        protected void SetupAndRunTest(JsonDrivenTestCase testCase)
        {
            Logger.LogDebug("Running {0}", testCase.Name);

            CheckServerRequirements(testCase.Shared);
            SetupAndRunTest(testCase.Shared, testCase.Test);
        }

        protected void VerifyCollectionData(
            IEnumerable<BsonDocument> expectedDocuments,
            string databaseName,
            string collectionName,
            Action<BsonDocument, BsonDocument> prepareExpectedResult)
        {
            var database = DriverTestConfiguration.Client.GetDatabase(databaseName).WithReadConcern(ReadConcern.Default);
            var collection = database.GetCollection<BsonDocument>(collectionName);
            var actualDocuments = collection.Find("{}").ToList();
            if (prepareExpectedResult != null)
            {
                var n = Math.Min(actualDocuments.Count, expectedDocuments.Count());
                for (var index = 0; index < n; index++)
                {
                    var actualEvent = actualDocuments.ElementAt(index);

                    var expectedEvent = expectedDocuments.ElementAt(index);
                    prepareExpectedResult(actualEvent, expectedEvent);
                }
            }
            actualDocuments.Should().BeEquivalentToWithComparer(expectedDocuments, BsonValueEquivalencyComparer.Instance);
        }

        // private methods
        private ReadPreference ReadPreferenceFromBsonValue(BsonValue value)
        {
            if (value.BsonType == BsonType.String)
            {
                var mode = (ReadPreferenceMode)Enum.Parse(typeof(ReadPreferenceMode), value.AsString, ignoreCase: true);
                return new ReadPreference(mode);
            }

            return ReadPreference.FromBsonDocument(value.AsBsonDocument);
        }

        private void SetupAndRunTest(BsonDocument shared, BsonDocument test)
        {
            JsonDrivenHelper.EnsureAllFieldsAreValid(shared, ExpectedSharedColumns);
            JsonDrivenHelper.EnsureAllFieldsAreValid(test, ExpectedTestColumns);

            if (test.Contains(SkipReasonKey))
            {
                throw new SkipException(test[SkipReasonKey].AsString);
            }

            Ensure.IsNotNullOrEmpty(DatabaseNameKey, nameof(DatabaseNameKey));
            Ensure.IsNotNullOrEmpty(CollectionNameKey, nameof(CollectionNameKey));

            _async = test["async"].AsBoolean;
            CustomDataValidation(shared, test);

            DatabaseName = GetDatabaseName(shared);
            CollectionName = GetCollectionName(shared);

            var client = CreateClientForTestSetup();
            TestInitialize(client, test, shared);
            DropCollection(client, DatabaseName, CollectionName, test, shared);
            CreateCollection(client, DatabaseName, CollectionName, test, shared);
            InsertData(client, DatabaseName, CollectionName, shared);

            using (ConfigureFailPoint(test, client)) // should be configured before the test client
            {
                EventCapturer eventCapturer = null;
                if (ShouldEventsBeChecked)
                {
                    eventCapturer = InitializeEventCapturer(new EventCapturer());
                }

                RunTest(shared, test, eventCapturer);
                if (ShouldEventsBeChecked)
                {
                    AssertEvents(eventCapturer, test);
                }

                AssertOutcome(test);
            }
        }
    }
}
