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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.TestHelpers.JsonDrivenTests;
using MongoDB.Driver.Authentication.External;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.Operations;
using MongoDB.Driver.Core.TestHelpers;
using MongoDB.Driver.Core.TestHelpers.Logging;
using MongoDB.Driver.Core.TestHelpers.XunitExtensions;
using MongoDB.Driver.Encryption;
using MongoDB.Driver.TestHelpers;
using MongoDB.TestHelpers.XunitExtensions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using Reflector = MongoDB.Bson.TestHelpers.Reflector;
using OperatingSystemHelper  = MongoDB.Driver.Core.Misc.OperatingSystemHelper;
using OperatingSystemPlatform  = MongoDB.Driver.Core.Misc.OperatingSystemPlatform;

namespace MongoDB.Driver.Tests.Specifications.client_side_encryption.prose_tests
{
    [Trait("Category", "CSFLE")]
    [Trait("Category", "Integration")]
    public class ClientEncryptionProseTests : LoggableTestClass
    {
        #region static
        private static readonly CollectionNamespace __collCollectionNamespace = CollectionNamespace.FromFullName("db.coll");
        private static readonly CollectionNamespace __keyVaultCollectionNamespace = CollectionNamespace.FromFullName("keyvault.datakeys");
        #endregion

        private const string SchemaMap =
            @"{
                ""db.coll"": {
                    ""bsonType"": ""object"",
                    ""properties"": {
                        ""encrypted_placeholder"": {
                            ""encrypt"": {
                                ""keyId"": ""/placeholder"",
                                ""bsonType"": ""string"",
                                ""algorithm"": ""AEAD_AES_256_CBC_HMAC_SHA_512-Random""
                              }
                          }
                      }
                  }
            }";

        private readonly IClusterInternal _cluster;

        // public constructors
        public ClientEncryptionProseTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _cluster = CoreTestConfiguration.Cluster;

            CoreTestConfiguration.SkipMongocryptdTests_SERVER_106469(checkForSharedLib: true);
        }

        // public methods
        [Theory]
        [ParameterAttributeData]
        public void AutomaticDataEncryptionKeysTest(
            [Values("aws", "local")] string kmsProvider,
            [Range(1, 4)] int testCase,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.Csfle2QEv2).ClusterTypes(ClusterType.ReplicaSet, ClusterType.Sharded, ClusterType.LoadBalanced);

            using (var client = ConfigureClient())
            using (var clientEncryption = ConfigureClientEncryption(client, kmsProviderFilter: kmsProvider))
            {
                var encryptedFields = BsonDocument.Parse(@"
                {
                    fields:
                    [{
                        path: 'ssn',
                        bsonType: 'string',
                        keyId: null
                    }]
                }");

                DropCollection(__collCollectionNamespace, encryptedFields);

                RunTestCase(testCase);

                void RunTestCase(int testCase)
                {
                    switch (testCase)
                    {
                        case 1: // Case 1: Simple Creation and Validation
                            {
                                // masterKey will be assigned automatically
                                var collection = CreateEncryptedCollection(client, clientEncryption, __collCollectionNamespace, encryptedFields, kmsProvider, async, out _);

                                var exception = Record.Exception(() => Insert(collection, async, new BsonDocument("ssn", "123-45-6789")));
                                exception.Should().BeOfType<MongoBulkWriteException<BsonDocument>>().Which.Message.Should().Contain("Document failed validation");
                            }
                            break;
                        case 2: // Case 2: Missing ``encryptedFields``
                            {
                                var exception = Record.Exception(() => CreateEncryptedCollection(client, clientEncryption, __collCollectionNamespace, encryptedFields: null, kmsProvider, async, out _));

                                exception
                                    .Should().BeOfType<MongoEncryptionCreateCollectionException>().Which.InnerException
                                    .Should().BeOfType<InvalidOperationException>().Which.Message.Should().Contain("There are no encrypted fields defined for the collection.") ;
                            }
                            break;
                        case 3: // Case 3: Invalid ``keyId``
                            {
                                var effectiveEncryptedFields = encryptedFields.DeepClone();
                                effectiveEncryptedFields["fields"].AsBsonArray[0].AsBsonDocument["keyId"] = false;
                                var exception = Record.Exception(() => CreateEncryptedCollection(client, clientEncryption, __collCollectionNamespace, effectiveEncryptedFields.AsBsonDocument, kmsProvider, async, out _));
                                exception
                                    .Should().BeOfType<MongoEncryptionCreateCollectionException>().Which.InnerException
                                    .Should().BeOfType<MongoCommandException>().Which.Message.Should().Contain("BSON field 'create.encryptedFields.fields.keyId' is the wrong type 'bool', expected type 'binData'");
                            }
                            break;
                       case 4: // Case 4: Insert encrypted value
                            {
                                var createCollectionOptions = new CreateCollectionOptions { EncryptedFields = encryptedFields };
                                var collection = CreateEncryptedCollection<BsonDocument>(client, clientEncryption, __collCollectionNamespace, createCollectionOptions, kmsProvider, async, out var effectiveEncryptedFields);
                                var dataKey = effectiveEncryptedFields["fields"].AsBsonArray[0].AsBsonDocument["keyId"].AsGuid; // get generated datakey
                                var encryptedValue = ExplicitEncrypt(clientEncryption, new EncryptOptions(algorithm: EncryptionAlgorithm.Unindexed, keyId: dataKey), "123-45-6789", async); // use explicit encryption to encrypt data before inserting
                                Insert(collection, async, new BsonDocument("ssn", encryptedValue));
                            }
                            break;
                        default: throw new Exception($"Unexpected test case {testCase}.");
                    }
                }
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void BsonSizeLimitAndBatchSizeSplittingTest(
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            var eventCapturer = CreateEventCapturer(commandNameFilter: "insert");
            using (var client = ConfigureClient())
            using (var clientEncrypted = ConfigureClientEncrypted(kmsProviderFilter: "local", eventCapturer: eventCapturer))
            {
                var collLimitSchema = JsonFileReader.Instance.Documents["limits.limits-schema.json"];
                CreateCollection(client, __collCollectionNamespace, new BsonDocument("$jsonSchema", collLimitSchema));
                var datakeysLimitsKey = JsonFileReader.Instance.Documents["limits.limits-key.json"];
                var keyVaultCollection = GetCollection(client, __keyVaultCollectionNamespace);
                Insert(keyVaultCollection, async, datakeysLimitsKey);

                var coll = GetCollection(clientEncrypted, __collCollectionNamespace);

                var exception = Record.Exception(
                    () => Insert(
                        coll,
                        async,
                        new BsonDocument
                        {
                            { "_id", "over_2mib_under_16mib" },
                            { "unencrypted", new string('a', 2097152) }
                        }));
                exception.Should().BeNull();
                eventCapturer.Clear();

                var limitsDoc = JsonFileReader.Instance.Documents["limits.limits-doc.json"];
                limitsDoc.AddRange(
                    new BsonDocument
                    {
                        {"_id", "encryption_exceeds_2mib"},
                        {"unencrypted", new string('a', 2097152 - 2000)}
                    });
                exception = Record.Exception(
                    () => Insert(
                        coll,
                        async,
                        limitsDoc));
                exception.Should().BeNull();
                eventCapturer.Clear();

                exception = Record.Exception(
                    () => Insert(
                        coll,
                        async,
                        new BsonDocument
                        {
                            { "_id", "over_2mib_1" },
                            { "unencrypted", new string('a', 2097152) }
                        },
                        new BsonDocument
                        {
                            { "_id", "over_2mib_2" },
                            { "unencrypted", new string('a', 2097152) }
                        }));
                exception.Should().BeNull();
                eventCapturer.Count.Should().Be(2);
                eventCapturer.Clear();

                var limitsDoc1 = JsonFileReader.Instance.Documents["limits.limits-doc.json"];
                limitsDoc1.AddRange(
                    new BsonDocument
                    {
                        { "_id", "encryption_exceeds_2mib_1" },
                        { "unencrypted", new string('a', 2097152 - 2000) }
                    });
                var limitsDoc2 = JsonFileReader.Instance.Documents["limits.limits-doc.json"];
                limitsDoc2.AddRange(
                    new BsonDocument
                    {
                        { "_id", "encryption_exceeds_2mib_2" },
                        { "unencrypted", new string('a', 2097152 - 2000) }
                    });

                exception = Record.Exception(
                    () => Insert(
                        coll,
                        async,
                        limitsDoc1,
                        limitsDoc2));
                exception.Should().BeNull();
                eventCapturer.Count.Should().Be(2);
                eventCapturer.Clear();

                exception = Record.Exception(
                    () => Insert(
                        coll,
                        async,
                        new BsonDocument
                        {
                            { "_id", "under_16mib" },
                            { "unencrypted", new string('a', 16777216 - 2000) }
                        }));
                exception.Should().BeNull();
                eventCapturer.Clear();

                limitsDoc = JsonFileReader.Instance.Documents["limits.limits-doc.json"];
                limitsDoc.AddRange(
                    new BsonDocument
                    {
                        {"_id", "encryption_exceeds_16mib"},
                        {"unencrypted", new string('a', 16777216 - 2000)}
                    });
                exception = Record.Exception(
                    () => Insert(
                        coll,
                        async,
                        limitsDoc));
                exception.Should().NotBeNull();
                eventCapturer.Clear();

                // additional not spec tests
                exception = Record.Exception(
                    () => Insert(
                        coll,
                        async,
                        new BsonDocument
                        {
                            { "_id", "advanced_over_2mib_1" },
                            { "unencrypted", new string('a', 2097152) }
                        },
                        new BsonDocument
                        {
                            { "_id", "advanced_over_2mib_2" },
                            { "unencrypted", new string('a', 2097152) }
                        },
                        new BsonDocument
                        {
                            { "_id", "advanced_over_2mib_3" },
                            { "unencrypted", new string('a', 2097152) }
                        }));
                exception.Should().BeNull();
                eventCapturer.Count.Should().Be(3);
                eventCapturer.Clear();

                exception = Record.Exception(
                    () => Insert(
                        coll,
                        async,
                        new BsonDocument
                        {
                            { "_id", "small_1" },
                            { "unencrypted", "a" }
                        },
                        new BsonDocument
                        {
                            { "_id", "small_2" },
                            { "unencrypted", "a" }
                        },
                        new BsonDocument
                        {
                            { "_id", "small_3" },
                            { "unencrypted", "a" }
                        }));
                exception.Should().BeNull();
                eventCapturer.Count.Should().Be(1);
                eventCapturer.Clear();
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void BypassMongocryptdClientWhenSharedLibraryTest(
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);
            RequireEnvironment.Check().EnvironmentVariable("CRYPT_SHARED_LIB_PATH", isDefined: true, allowEmpty: false);
            // socket.Close can hang on non windows OS. Might be related to this issue: https://github.com/dotnet/runtime/issues/47342
            RequirePlatform
                .Check()
                .SkipWhen(SupportedOperatingSystem.Linux)
                .SkipWhen(SupportedOperatingSystem.MacOS);

            const int mongocryptPort = 27030;
            var timeout = TimeSpan.FromSeconds(3);
            var extraOptions = new Dictionary<string, object>
            {
                { "mongocryptdURI", $"mongodb://localhost:{mongocryptPort}" }
            };

            var mongocryptdIpAddress = IPAddress.Parse("127.0.0.1");
            TcpListener tcpListener = null;
            try
            {
                tcpListener = new TcpListener(mongocryptdIpAddress, port: mongocryptPort);
                tcpListener.Start();
                var listenerThread = new Thread(ThreadStart) { IsBackground = true };
                listenerThread.Start(tcpListener);

                using (var clientEncrypted = ConfigureClientEncrypted(kmsProviderFilter: "local", extraOptions: extraOptions))
                {
                    var coll = GetCollection(clientEncrypted, __collCollectionNamespace);

                    _ = Record.Exception(() => Insert(coll, async, new BsonDocument("unencrypted", "test")));
                }

                if (listenerThread.Join(timeout))
                {
                    // This exception is never thrown when mognocryptd mongoClient is not spawned which is expected behavior.
                    // However, if we intentionally break that logic to spawn mongocryptd mongoClient regardless of shared library,
                    // this exception sometimes won't be thrown. In all such cases the spent time in listenerThread.Join is higher
                    // or really close to timeout. So it's unclear why Join doesn't throw in that cases, but that logic is unrelated
                    // to the driver and csfle in particular. We rely on the fact that even if we break this logic,
                    // we run this test more than once.
                    throw new Exception($"Listener accepted a tcp call for moncgocryptd during {timeout}.");
                }
            }
            finally
            {
                tcpListener?.Stop();
            }

            void ThreadStart(object param)
            {
                try
                {
                    var tcpListener = (TcpListener)param;
                    using var client = tcpListener.AcceptTcpClient();
                    // Perform a blocking call to accept requests.
                    // if we're here, then something queries port 27030.
                }
                catch (SocketException)
                {
                    // listener stopped outside thread
                }
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void BypassSpawningMongocryptdViaMongocryptdBypassSpawnTest(
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);
            RequireEnvironment.Check().EnvironmentVariable("CRYPT_SHARED_LIB_PATH", isDefined: false);

            var extraOptions = new Dictionary<string, object>
            {
                { "mongocryptdBypassSpawn", true },
                { "mongocryptdURI", "mongodb://localhost:27021/db?serverSelectionTimeoutMS=10000" },
                { "mongocryptdSpawnArgs", new [] { "--pidfilepath=bypass-spawning-mongocryptd.pid", "--port=27021" } },
            };
            var clientEncryptedSchema = new BsonDocument("db.coll", JsonFileReader.Instance.Documents["external.external-schema.json"]);
            using (var client = ConfigureClient())
            using (var clientEncrypted = ConfigureClientEncrypted(
                schemaMap: clientEncryptedSchema,
                kmsProviderFilter: "local",
                extraOptions: extraOptions))
            {
                var coll = GetCollection(clientEncrypted, __collCollectionNamespace);
                var exception = Record.Exception(() => Insert(coll, async, new BsonDocument("encrypted", "test")));

                AssertInnerEncryptionExceptionRegex<TimeoutException>(exception, "A timeout occurred after \\d+ms selecting a server");
            }
        }

        public enum BypassSpawningMongocryptd
        {
            BypassAutoEncryption,
            BypassQueryAnalysis,
            SharedLibrary
        }

        [Theory]
        [ParameterAttributeData]
        public void BypassSpawningMongocryptdTest(
            [Values(BypassSpawningMongocryptd.BypassQueryAnalysis, BypassSpawningMongocryptd.BypassAutoEncryption, BypassSpawningMongocryptd.SharedLibrary)] BypassSpawningMongocryptd bypassSpawning,
            [Values(false, true)] bool async)
        {
            using (var clientEncrypted = EnsureEnvironmentAndConfigureTestClientEncrypted())
            using (var mongocryptdClient = new MongoClient("mongodb://localhost:27021/?serverSelectionTimeoutMS=1000"))
            {
                var coll = GetCollection(clientEncrypted, __collCollectionNamespace);
                Insert(coll, async, new BsonDocument("unencrypted", "test"));

                var adminDatabase = mongocryptdClient.GetDatabase(DatabaseNamespace.Admin.DatabaseName);
                var legacyHelloCommand = new BsonDocument(OppressiveLanguageConstants.LegacyHelloCommandName, 1);
                var exception = Record.Exception(() => adminDatabase.RunCommand<BsonDocument>(legacyHelloCommand));

                exception.Should().BeOfType<TimeoutException>();
                exception.Message.Should().MatchRegex(@".*A timeout occurred after \d+ms selecting a server.*").And.Contain("localhost:27021");
            }

            IMongoClient EnsureEnvironmentAndConfigureTestClientEncrypted()
            {
                var extraOptions = new Dictionary<string, object>
                {
                    { "mongocryptdSpawnArgs", new [] { "--pidfilepath=bypass-spawning-mongocryptd.pid", "--port=27021" } },
                };
                var kmsProvider = "local";
                switch (bypassSpawning)
                {
                    case BypassSpawningMongocryptd.BypassAutoEncryption:
                        RequireServer.Check().Supports(Feature.ClientSideEncryption);
                        RequireEnvironment.Check().EnvironmentVariable("CRYPT_SHARED_LIB_PATH", isDefined: false);
                        return ConfigureClientEncrypted(kmsProviderFilter: kmsProvider, bypassAutoEncryption: true, extraOptions: extraOptions);
                    case BypassSpawningMongocryptd.BypassQueryAnalysis:
                        RequireServer.Check().Supports(Feature.ClientSideEncryption);
                        RequireEnvironment.Check().EnvironmentVariable("CRYPT_SHARED_LIB_PATH", isDefined: false);
                        return ConfigureClientEncrypted(kmsProviderFilter: kmsProvider, bypassQueryAnalysis: true, extraOptions: extraOptions);
                    case BypassSpawningMongocryptd.SharedLibrary:
                        {
                            RequireServer.Check().Supports(Feature.Csfle2).ClusterTypes(ClusterType.ReplicaSet, ClusterType.Sharded, ClusterType.LoadBalanced);
                            RequireEnvironment.Check().EnvironmentVariable("CRYPT_SHARED_LIB_PATH", isDefined: true, allowEmpty: false);
                            var clientEncryptedSchema = new BsonDocument("db.coll", JsonFileReader.Instance.Documents["external.external-schema.json"]);
                            var cryptSharedPath = CoreTestConfiguration.GetCryptSharedLibPath();
                            Ensure.That(File.Exists(cryptSharedPath), $"Shared library path {cryptSharedPath} is not valid.");
                            var effectiveExtraOptions = new Dictionary<string, object>(extraOptions)
                            {
                                { "mongocryptdURI", "mongodb://localhost:27021/db?serverSelectionTimeoutMS=1000" },
                                { "cryptSharedLibPath", cryptSharedPath },
                                { "cryptSharedLibRequired", true }
                            };
                            return ConfigureClientEncrypted(kmsProviderFilter: kmsProvider, schemaMap: clientEncryptedSchema, extraOptions: effectiveExtraOptions);
                        }
                    default: throw new Exception($"Invalid bypass mongocryptd {bypassSpawning} option.");
                }
            }
        }

        [Fact]
        public void ConcreteTypeDeserializationTest()
        {
            RequireServer.Check().Supports(Feature.Csfle2QEv2).ClusterTypes(ClusterType.ReplicaSet, ClusterType.Sharded, ClusterType.LoadBalanced);

            using var client = ConfigureClient(keyVaultNamespace: __keyVaultCollectionNamespace, kmsProviders: EncryptionTestHelper.GetKmsProviders("local"));
            using var clientEncryption = ConfigureClientEncryption(client, kmsProviderFilter: "local");

            var datakeysCollection = GetCollection(client, __keyVaultCollectionNamespace);
            var externalKey = JsonFileReader.Instance.Documents["external.external-key.json"];
            datakeysCollection.InsertOne(externalKey);

            var encryptedFields = new BsonDocument
                {
                    {
                        "fields", new BsonArray
                        {
                            new BsonDocument
                            {
                                { "keyId", externalKey["_id"].AsBsonBinaryData },
                                { "path", "Ssn" },
                                { "bsonType", "string" },
                                { "queries", new BsonDocument("queryType", "equality") }
                            },
                        }
                    }
                };

            var collection = CreateEncryptedCollection<Patient>(client, clientEncryption, __collCollectionNamespace, encryptedFields, "local", false, out _);

            var patient = new Patient() { Name = "Name", Ssn = "14159265359" };
            collection.InsertOne(patient);

            var deserializedPatient = collection.Find(FilterDefinition<Patient>.Empty).ToList().Single();
            deserializedPatient.Ssn.Should().Be(patient.Ssn);
        }

        [Theory]
        [ParameterAttributeData]
        public void CorpusTest(
            [Values(false, true)] bool useLocalSchema,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            // this needs only for kmip, but the test design doesn't allow skipping only required steps
            RequireEnvironment.Check().EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: true);

            var corpusSchema = JsonFileReader.Instance.Documents["corpus.corpus-schema.json"];
            var schemaMap = useLocalSchema ? new BsonDocument("db.coll", corpusSchema) : null;
            using (var client = ConfigureClient())
            using (var clientEncrypted = ConfigureClientEncrypted(schemaMap))
            using (var clientEncryption = ConfigureClientEncryption(clientEncrypted))
            {
                CreateCollection(client, __collCollectionNamespace, new BsonDocument("$jsonSchema", corpusSchema));

                var keyVaultCollection = GetCollection(client, __keyVaultCollectionNamespace);
                Insert(
                    keyVaultCollection,
                    async,
                    JsonFileReader.Instance.Documents["corpus.corpus-key-local.json"],
                    JsonFileReader.Instance.Documents["corpus.corpus-key-aws.json"],
                    JsonFileReader.Instance.Documents["corpus.corpus-key-azure.json"],
                    JsonFileReader.Instance.Documents["corpus.corpus-key-gcp.json"],
                    JsonFileReader.Instance.Documents["corpus.corpus-key-kmip.json"]);

                var corpus = JsonFileReader.Instance.Documents["corpus.corpus.json"];
                var corpusCopied = new BsonDocument
                {
                    corpus.GetElement("_id"),
                    corpus.GetElement("altname_aws"),
                    corpus.GetElement("altname_local"),
                    corpus.GetElement("altname_azure"),
                    corpus.GetElement("altname_gcp"),
                    corpus.GetElement("altname_kmip")
                };

                foreach (var corpusElement in corpus.Elements.Where(c => c.Value.IsBsonDocument))
                {
                    var corpusValue = corpusElement.Value.DeepClone();
                    var kms = corpusValue["kms"].AsString;
                    var abbreviatedAlgorithmName = corpusValue["algo"].AsString;
                    var identifier = corpusValue["identifier"].AsString;

                    var allowed = corpusValue["allowed"].ToBoolean();
                    var value = corpusValue["value"];
                    var method = corpusValue["method"].AsString;
                    switch (method)
                    {
                        case "auto":
                            corpusCopied.Add(corpusElement);
                            continue;
                        case "explicit":
                            {
                                var encryptionOptions = CreateEncryptOptions(abbreviatedAlgorithmName, identifier, kms);
                                BsonBinaryData encrypted = null;
                                var exception = Record.Exception(() =>
                                {
                                    encrypted = ExplicitEncrypt(
                                        clientEncryption,
                                        encryptionOptions,
                                        value,
                                        async);
                                });
                                if (allowed)
                                {
                                    exception.Should().BeNull();
                                    encrypted.Should().NotBeNull();
                                    corpusValue["value"] = encrypted;
                                }
                                else
                                {
                                    exception.Should().NotBeNull();
                                }
                                corpusCopied.Add(new BsonElement(corpusElement.Name, corpusValue));
                            }
                            break;
                        default:
                            throw new ArgumentException($"Unsupported method name {method}.", nameof(method));
                    }
                }

                var coll = GetCollection(clientEncrypted, __collCollectionNamespace);
                Insert(coll, async, corpusCopied);

                var corpusDecrypted = Find(coll, new BsonDocument(), async).Single();
                corpusDecrypted.Should().Be(corpus);

                var corpusEncryptedExpected = JsonFileReader.Instance.Documents["corpus.corpus-encrypted.json"];
                coll = GetCollection(client, __collCollectionNamespace);
                var corpusEncryptedActual = Find(coll, new BsonDocument(), async).Single();
                foreach (var expectedElement in corpusEncryptedExpected.Elements.Where(c => c.Value.IsBsonDocument))
                {
                    var expectedElementValue = expectedElement.Value;
                    var expectedAlgorithm = ParseAlgorithm(expectedElementValue["algo"].AsString);
                    var expectedAllowed = expectedElementValue["allowed"].ToBoolean();
                    var expectedValue = expectedElementValue["value"];
                    var actualValue = corpusEncryptedActual.GetValue(expectedElement.Name)["value"];

                    switch (expectedAlgorithm)
                    {
                        case EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic:
                            actualValue.Should().Be(expectedValue);
                            break;
                        case EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Random:
                            if (expectedAllowed)
                            {
                                actualValue.Should().NotBe(expectedValue);
                            }
                            break;
                        default:
                            throw new ArgumentException($"Unsupported expected algorithm {expectedAlgorithm}.", nameof(expectedAlgorithm));
                    }

                    if (expectedAllowed)
                    {
                        var actualDecryptedValue = ExplicitDecrypt(clientEncryption, actualValue.AsBsonBinaryData, async);
                        var expectedDecryptedValue = ExplicitDecrypt(clientEncryption, expectedValue.AsBsonBinaryData, async);
                        actualDecryptedValue.Should().Be(expectedDecryptedValue);
                    }
                    else
                    {
                        actualValue.Should().Be(expectedValue);
                    }
                }
            }

            EncryptOptions CreateEncryptOptions(string algorithm, string identifier, string kms)
            {
                Guid? keyId = null;
                string alternateName = null;
                switch (identifier)
                {
                    case "id":
                        keyId = kms switch
                        {
                            "local" => GuidConverter.FromBytes(Convert.FromBase64String("LOCALAAAAAAAAAAAAAAAAA=="), GuidRepresentation.Standard),
                            "aws" => GuidConverter.FromBytes(Convert.FromBase64String("AWSAAAAAAAAAAAAAAAAAAA=="), GuidRepresentation.Standard),
                            "azure" => GuidConverter.FromBytes(Convert.FromBase64String("AZUREAAAAAAAAAAAAAAAAA=="), GuidRepresentation.Standard),
                            "gcp" => GuidConverter.FromBytes(Convert.FromBase64String("GCPAAAAAAAAAAAAAAAAAAA=="), GuidRepresentation.Standard),
                            "kmip" => GuidConverter.FromBytes(Convert.FromBase64String("KMIPAAAAAAAAAAAAAAAAAA=="), GuidRepresentation.Standard),
                            _ => throw new ArgumentException($"Unsupported kms type {kms}."),
                        };
                        break;
                    case "altname":
                        alternateName = kms;
                        break;
                    default:
                        throw new ArgumentException($"Unsupported identifier {identifier}.", nameof(identifier));
                }

                return new EncryptOptions(ParseAlgorithm(algorithm).ToString(), alternateName, keyId);
            }

            EncryptionAlgorithm ParseAlgorithm(string algorithm) => algorithm switch
            {
                "rand" => EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Random,
                "det" => EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic,
                _ => throw new ArgumentException($"Unsupported algorithm {algorithm}."),
            };
        }

        [Theory]
        [ParameterAttributeData]
        public void CreateDataKeyAndDoubleEncryptionTest(
            [Values("local", "aws", "azure", "gcp", "kmip")] string kmsProvider,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            if (kmsProvider == "kmip")
            {
                RequireEnvironment.Check().EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: true);
            }

            using (var client = ConfigureClient())
            using (var clientEncrypted = ConfigureClientEncrypted(BsonDocument.Parse(SchemaMap), kmsProviderFilter: kmsProvider))
            using (var clientEncryption = ConfigureClientEncryption(clientEncrypted, kmsProviderFilter: kmsProvider))
            {
                var dataKeyOptions = CreateDataKeyOptions(kmsProvider);
                var dataKey = CreateDataKey(clientEncryption, kmsProvider, dataKeyOptions, async);

                var keyVaultCollection = GetCollection(client, __keyVaultCollectionNamespace);
                var keyVaultDocument =
                    Find(
                        keyVaultCollection,
                        new BsonDocument("_id", new BsonBinaryData(dataKey, GuidRepresentation.Standard)),
                        async)
                    .Single();
                keyVaultDocument["masterKey"]["provider"].Should().Be(BsonValue.Create(kmsProvider));

                var encryptOptions = new EncryptOptions(
                    EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic.ToString(),
                    keyId: dataKey);

                var encryptedValue = ExplicitEncrypt(
                    clientEncryption,
                    encryptOptions,
                    $"hello {kmsProvider}",
                    async);
                encryptedValue.SubType.Should().Be(BsonBinarySubType.Encrypted);

                var coll = GetCollection(clientEncrypted, __collCollectionNamespace);
                Insert(
                    coll,
                    async,
                    new BsonDocument
                    {
                        {"_id", kmsProvider},
                        {"value", encryptedValue}
                    });

                var findResult = Find(coll, new BsonDocument("_id", kmsProvider), async).Single();
                findResult["value"].ToString().Should().Be($"hello {kmsProvider}");

                encryptOptions = new EncryptOptions(
                    EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic.ToString(),
                    alternateKeyName: $"{kmsProvider}_altname");
                var encryptedValueWithAlternateKeyName = ExplicitEncrypt(
                    clientEncryption,
                    encryptOptions,
                    $"hello {kmsProvider}",
                    async);
                encryptedValueWithAlternateKeyName.SubType.Should().Be(BsonBinarySubType.Encrypted);
                encryptedValueWithAlternateKeyName.Should().Be(encryptedValue);

                if (kmsProvider == "local") // the test description expects this assert only once for a local kms provider
                {
                    coll = GetCollection(clientEncrypted, __collCollectionNamespace);
                    var exception = Record.Exception(() => Insert(coll, async, new BsonDocument("encrypted_placeholder", encryptedValue)));
                    exception.Should().BeOfType<MongoEncryptionException>();
                }
            }
        }

        [Theory]
        // aws
        [InlineData("aws", null, null, null)]
        [InlineData("aws", "kms.us-east-1.amazonaws.com", null, null)]
        [InlineData("aws", "kms.us-east-1.amazonaws.com:443", null, null)]
        [InlineData("kmip", "localhost:12345", "$ConnectionRefused$", null)]
        [InlineData("aws", "kms.us-east-2.amazonaws.com", "_GenericCryptException_", null)]
        [InlineData("aws", "doesnotexist.invalid", "$HostNotFound,TryAgain$", null)]
        // additional not spec tests
        [InlineData("aws", "$test$", "Invalid endpoint, expected dot separator in host, but got: $test$", null)]
        // azure
        [InlineData("azure", "key-vault-csfle.vault.azure.net", null, "$HostNotFound,TryAgain$")]
        // gcp
        [InlineData("gcp", "cloudkms.googleapis.com:443", null, "$HostNotFound,TryAgain$")]
        [InlineData("gcp", "doesnotexist.invalid:443", "Invalid KMS response", null)]
        // kmip
        [InlineData("kmip", null, null, "$HostNotFound,TryAgain$")]
        [InlineData("kmip", "localhost:5698", null, null)]
        [InlineData("kmip", "doesnotexist.invalid:5698", "$HostNotFound,TryAgain$", null)]
        public void CustomEndpointTest(
            string kmsType,
            string customEndpoint,
            string expectedExceptionInfoForValidEncryption,
            string expectedExceptionInfoForInvalidEncryption)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            if (kmsType == "kmip")
            {
                RequireEnvironment.Check().EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: true);
            }

            using (var client = ConfigureClient())
            using (var clientEncryption = ConfigureClientEncryption(client, ValidKmsEndpointConfigurator, kmsProviderFilter: kmsType))
            using (var clientEncryptionInvalid = ConfigureClientEncryption(client, InvalidKmsEndpointConfigurator, kmsProviderFilter: kmsType))
            {
                var testCaseMasterKey = kmsType switch
                {
                    "aws" => new BsonDocument
                    {
                        { "region", "us-east-1" },
                        { "key", "arn:aws:kms:us-east-1:579766882180:key/89fcc2c4-08b0-4bd9-9f25-e30687b580d0" },
                        { "endpoint", customEndpoint, customEndpoint != null }
                    },
                    "azure" => new BsonDocument
                    {
                        { "keyVaultEndpoint", customEndpoint },
                        { "keyName", "key-name-csfle" }
                    },
                    "gcp" => new BsonDocument
                    {
                        { "projectId", "devprod-drivers" },
                        { "location", "global" },
                        { "keyRing", "key-ring-csfle" },
                        { "keyName", "key-name-csfle" },
                        { "endpoint", customEndpoint }
                    },
                    "kmip" => new BsonDocument
                    {
                        { "keyId", "1" },
                        { "endpoint", customEndpoint, customEndpoint != null }
                    },
                    _ => throw new Exception($"Unexpected kms type {kmsType}."),
                };
                foreach (var async in new[] { false, true })
                {
                    var exception = Record.Exception(() => TestCase(clientEncryption, testCaseMasterKey, async));
                    AssertResult(exception, expectedExceptionInfoForValidEncryption);
                    if (expectedExceptionInfoForInvalidEncryption != null)
                    {
                        exception = Record.Exception(() => CreateDataKeyTestCaseStep(clientEncryptionInvalid, testCaseMasterKey, async));
                        AssertResult(exception, expectedExceptionInfoForInvalidEncryption);
                    }
                }

            }

            void AssertResult(Exception ex, string expectedExceptionInfo)
            {
                if (expectedExceptionInfo != null)
                {
                    var innerException = ex.Should().BeOfType<MongoEncryptionException>().Subject.InnerException;

                    if (expectedExceptionInfo.StartsWith("$") &&
                        expectedExceptionInfo.EndsWith("$"))
                    {
                        var expectedValues = expectedExceptionInfo
                            .Trim('$')
                            .Split(',')
                            .Select(v => Enum.Parse(typeof(SocketError), v))
                            .ToArray();

                        var e = innerException.Should().BeAssignableTo<SocketException>().Subject;// kmip triggers driver side exception
                        expectedValues.Should().Contain(e.SocketErrorCode);// the error message is platform dependent
                    }
                    else
                    {
                        var e = innerException.Should().BeOfType<CryptException>().Subject;

                        if (expectedExceptionInfo != "_GenericCryptException_")
                        {
                            e.Message.Should().Contain(expectedExceptionInfo.ToString());
                        }
                    }
                }
                else
                {
                    ex.Should().BeNull();
                }
            }

            Guid CreateDataKeyTestCaseStep(ClientEncryption testCaseClientEncryption, BsonDocument masterKey, bool async)
            {
                var dataKeyOptions = new DataKeyOptions(masterKey: masterKey);
                return CreateDataKey(testCaseClientEncryption, kmsType, dataKeyOptions, async);
            }

            void InvalidKmsEndpointConfigurator(string kt, Dictionary<string, object> ko)
            {
                switch (kt)
                {
                    case "azure":
                        ko.Add("identityPlatformEndpoint", "doesnotexist.invalid:443");
                        break;
                    case "gcp":
                        ko.Add("endpoint", "doesnotexist.invalid:443");
                        break;
                    case "kmip":
                        AddOrReplace(ko, "endpoint", "doesnotexist.invalid:5698");
                        break;
                }
            }

            void ValidKmsEndpointConfigurator(string kt, Dictionary<string, object> ko)
            {
                switch (kt)
                {
                    // these values are default, so set them just to show the difference with incorrect values
                    // NOTE: "aws" and "local" don't have a way to set endpoints here
                    case "azure":
                        ko.Add("identityPlatformEndpoint", "login.microsoftonline.com:443");
                        break;
                    case "gcp":
                        ko.Add("endpoint", "oauth2.googleapis.com:443");
                        break;
                    case "kmip":
                        // do nothing
                        break;
                }
            }

            void TestCase(ClientEncryption testCaseClientEncryption, BsonDocument masterKey, bool async)
            {
                var dataKey = CreateDataKeyTestCaseStep(testCaseClientEncryption, masterKey, async);
                var encryptOptions = new EncryptOptions(
                    algorithm: EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic.ToString(),
                    keyId: dataKey);
                var value = "test";
                var encrypted = ExplicitEncrypt(testCaseClientEncryption, encryptOptions, value, async);
                var decrypted = ExplicitDecrypt(testCaseClientEncryption, encrypted, async);
                decrypted.Should().Be(BsonValue.Create(value));
            }
        }

        [Theory]
        [MemberData(nameof(DeadlockTest_MemberData))]
        public void DeadlockTest(
            string _,
            int maxPoolSize,
            bool bypassAutoEncryption,
            string keyVaultMongoClientKey,
            int expectedNumberOfClients,
            string[] clientEncryptedEventsExpectation,
            string[] clientKeyVaultEventsExpectation,
            bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            var clientKeyVaultEventCapturer = CreateEventCapturer();
            using (var client_keyvault = CreateMongoClient(maxPoolSize: 1, writeConcern: WriteConcern.WMajority, readConcern: ReadConcern.Majority, eventCapturer: clientKeyVaultEventCapturer))
            using (var client_test = ConfigureClient(clearCollections: true, writeConcern: WriteConcern.WMajority, readConcern: ReadConcern.Majority))
            {
                var dataKeysCollection = GetCollection(client_test, __keyVaultCollectionNamespace);
                var externalKey = JsonFileReader.Instance.Documents["external.external-key.json"];
                Insert(dataKeysCollection, async, externalKey);

                var externalSchema = JsonFileReader.Instance.Documents["external.external-schema.json"];
                CreateCollection(client_test, __collCollectionNamespace, new BsonDocument("$jsonSchema", externalSchema));

                using (var client_encryption = ConfigureClientEncryption(client_test, kmsProviderFilter: "local"))
                {
                    var value = "string0";
                    var encryptionOptions = new EncryptOptions(
                        algorithm: EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic.ToString(),
                        alternateKeyName: "local");
                    var ciphertext = ExplicitEncrypt(client_encryption, encryptionOptions, value, async);

                    var eventCapturer = CreateEventCapturer().Capture<ClusterOpeningEvent>();

                    using (var client_encrypted = ConfigureClientEncrypted(
                        kmsProviderFilter: "local",
                        maxPoolSize: maxPoolSize,
                        bypassAutoEncryption: bypassAutoEncryption,
                        eventCapturer: eventCapturer,
                        externalKeyVaultClient: GetKeyVaultMongoClientByKey()))
                    {
                        IMongoCollection<BsonDocument> collCollection;
                        if (client_encrypted.Settings.AutoEncryptionOptions.BypassAutoEncryption)
                        {
                            collCollection = GetCollection(client_test, __collCollectionNamespace);
                            Insert(
                                collCollection,
                                async,
                                new BsonDocument
                                {
                                    { "_id", 0 },
                                    { "encrypted", ciphertext }
                                });
                        }
                        else
                        {
                            collCollection = GetCollection(client_encrypted, __collCollectionNamespace);
                            Insert(
                                collCollection,
                                async,
                                new BsonDocument
                                {
                                    { "_id", 0 },
                                    { "encrypted", value }
                                });
                        }

                        collCollection = GetCollection(client_encrypted, __collCollectionNamespace);
                        var findResult = Find(collCollection, BsonDocument.Parse("{ _id : 0 }"), async).Single();
                        findResult.Should().Be($"{{ _id : 0, encrypted : '{value}' }}");
                        var events = eventCapturer.Events.ToList();
                        AssertEvents(events.OfType<CommandStartedEvent>(), clientEncryptedEventsExpectation);
                        if (clientKeyVaultEventsExpectation != null)
                        {
                            AssertEvents(clientKeyVaultEventCapturer.Events.OfType<CommandStartedEvent>(), clientKeyVaultEventsExpectation);
                        }

                        AssertNumberOfClients(events.OfType<ClusterOpeningEvent>());
                    }
                }

                IMongoClient GetKeyVaultMongoClientByKey()
                {
                    switch (keyVaultMongoClientKey)
                    {
                        case "client_keyvault":
                            return client_keyvault;
                        default:
                            return null;
                    }
                }
            }

            void AssertEvents(IEnumerable<CommandStartedEvent> events, string[] expectedEventsDetails)
            {
                for (int i = 0; i < expectedEventsDetails.Length; i++)
                {
                    var arguments = expectedEventsDetails[i].Split(';');
                    (string CommandName, string Database) expectedEventDetails = (arguments[0], arguments[1]);
                    var @event = events.ElementAt(i);
                    @event.DatabaseNamespace.DatabaseName.Should().Be(expectedEventDetails.Database);
                    @event.CommandName.Should().Be(expectedEventDetails.CommandName);
                }
                events.Count().Should().Be(expectedEventsDetails.Count());
            }

            void AssertNumberOfClients(IEnumerable<ClusterOpeningEvent> events)
            {
                events.Count().Should().Be(expectedNumberOfClients);
            }
        }

        public static IEnumerable<object[]> DeadlockTest_MemberData()
        {
            var testCases = new List<object[]>();
            testCases.AddRange(
                CasesWithAsync(
                    name: "case 1",
                    maxPoolSize: 1,
                    bypassAutoEncryption: false,
                    keyVaultMongoClient: null,
                    expectedNumberOfClients: 2,
                    clientEncryptedEventsExpectation:
                    new[]
                    {
                        "listCollections;db",
                        "find;keyvault",
                        "insert;db",
                        "find;db"
                    },
                    clientKeyVaultEventsExpectation: null));
            testCases.AddRange(
                CasesWithAsync(
                    name: "case 2",
                    maxPoolSize: 1,
                    bypassAutoEncryption: false,
                    keyVaultMongoClient: "client_keyvault",
                    expectedNumberOfClients: 2,
                    clientEncryptedEventsExpectation:
                    new[]
                    {
                        "listCollections;db",
                        "insert;db",
                        "find;db"
                    },
                    clientKeyVaultEventsExpectation:
                    new[]
                    {
                        "find;keyvault"
                    }));
            testCases.AddRange(
                CasesWithAsync(
                    name: "case 3",
                    maxPoolSize: 1,
                    bypassAutoEncryption: true,
                    keyVaultMongoClient: null,
                    expectedNumberOfClients: 2,
                    clientEncryptedEventsExpectation:
                    new[]
                    {
                        "find;db",
                        "find;keyvault"
                    },
                    clientKeyVaultEventsExpectation: null));
            testCases.AddRange(
                CasesWithAsync(
                    name: "case 4",
                    maxPoolSize: 1,
                    bypassAutoEncryption: true,
                    keyVaultMongoClient: "client_keyvault",
                    expectedNumberOfClients: 1,
                    clientEncryptedEventsExpectation:
                    new[]
                    {
                        "find;db",
                    },
                    clientKeyVaultEventsExpectation:
                    new[]
                    {
                        "find;keyvault"
                    }));

            // cases 5-8 use "MaxPoolSize: 0" which is not supported by the c# driver

            return testCases;

            IEnumerable<object[]> CasesWithAsync(
                string name,
                int maxPoolSize,
                bool bypassAutoEncryption,
                string keyVaultMongoClient,
                int expectedNumberOfClients,
                string[] clientEncryptedEventsExpectation,
                string[] clientKeyVaultEventsExpectation)
            {
                foreach (var async in new[] { true, false })
                {
                    yield return new object[] { name, maxPoolSize, bypassAutoEncryption, keyVaultMongoClient, expectedNumberOfClients, clientEncryptedEventsExpectation, clientKeyVaultEventsExpectation, async };
                }
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void DecryptionEvents(
            [Range(1, 4)] int testCase,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            var decryptionEventsCollectionNamespace = CollectionNamespace.FromFullName("db.decryption_events");
            using (var setupClient = ConfigureClient(clearCollections: true, mainCollectionNamespace: decryptionEventsCollectionNamespace))
            using (var clientEncryption = ConfigureClientEncryption(setupClient, kmsProviderFilter: "local"))
            {
                var keyId = CreateDataKey(clientEncryption, "local", new DataKeyOptions(), async);

                var value = "hello";
                var ciphertext = ExplicitEncrypt(clientEncryption, new EncryptOptions(algorithm: EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic, keyId: keyId), value, async);

                // Copy ciphertext into a variable named malformedCiphertext. Change the last byte. This will produce an invalid HMAC tag.
                var malformeLastByte = ciphertext.Bytes.Last();
                var malformedCiphertext = new BsonBinaryData(Enumerable.Append<byte>(ciphertext.Bytes.Take(ciphertext.Bytes.Length - 1), (byte)(malformeLastByte == 0 ? 1 : 0)).ToArray(), ciphertext.SubType);

                var eventCapturer = new EventCapturer()
                    .Capture<CommandSucceededEvent>(c => c.CommandName == "aggregate")
                    .Capture<CommandFailedEvent>(c => c.CommandName == "aggregate");
                using (var encryptedClient = ConfigureClientEncrypted(kmsProviderFilter: "local", retryReads: false, eventCapturer: eventCapturer))
                {
                    var decryptionEventsCollection = GetCollection(encryptedClient, decryptionEventsCollectionNamespace);
                    RunTestCase(decryptionEventsCollection, testCase, ciphertext, malformedCiphertext, eventCapturer);
                }
            }

            void RunTestCase(IMongoCollection<BsonDocument> decryptionEventsCollection, int testCase, BsonValue ciphertext, BsonValue malformedCiphertext, EventCapturer eventCapturer)
            {
                switch (testCase)
                {
                    case 1: // Case 1: Command Error
                        {
                            var failPointCommand = BsonDocument.Parse(@"
                            {
                                ""configureFailPoint"" : ""failCommand"",
                                ""mode"" : { ""times"" : 1 },
                                ""data"" :
                                {
                                    ""errorCode"" : 123,
                                    ""failCommands"": [ ""aggregate"" ]
                                }
                            }");
                            using (FailPoint.Configure(_cluster, NoCoreSession.NewHandle(), failPointCommand))
                            {
                                var exception = Record.Exception(() => Aggregate(decryptionEventsCollection, async));
                                exception.Should().BeOfType<MongoCommandException>();

                                eventCapturer.Next().Should().BeOfType<CommandFailedEvent>();
                                eventCapturer.Any().Should().BeFalse();
                            }
                        }
                        break;
                    case 2: // Case 2: Network Error
                        {
                            var failPointCommand = BsonDocument.Parse(@"
                            {
                                ""configureFailPoint"" : ""failCommand"",
                                ""mode"" : { ""times"" : 1 },
                                ""data"" :
                                {
                                    ""errorCode"" : 123,
                                    ""closeConnection"" : true,
                                    ""failCommands"" : [ ""aggregate"" ]
                                }
                            }");
                            using (FailPoint.Configure(_cluster, NoCoreSession.NewHandle(), failPointCommand))
                            {
                                var exception = Record.Exception(() => Aggregate(decryptionEventsCollection, async));
                                exception.Should().BeOfType<MongoConnectionException>().Which.IsNetworkException.Should().BeTrue();

                                eventCapturer.Next().Should().BeOfType<CommandFailedEvent>();
                                eventCapturer.Any().Should().BeFalse();
                            }
                        }
                        break;
                    case 3: // Case 3: Decrypt Error
                        {
                            Insert(decryptionEventsCollection, async, new BsonDocument("encrypted", malformedCiphertext));
                            var exception = Record.Exception(() => Aggregate(decryptionEventsCollection, async));
                            AssertInnerEncryptionException<CryptException>(exception, "HMAC validation failure");

                            var reply = eventCapturer.Next().Should().BeOfType<CommandSucceededEvent>().Which.Reply;
                            eventCapturer.Any().Should().BeFalse();
                            reply["cursor"]["firstBatch"].AsBsonArray.Single()["encrypted"].AsBsonBinaryData.SubType.Should().Be(BsonBinarySubType.Encrypted);
                        }
                        break;
                    case 4: // Case 4: Decrypt Success
                        {
                            Insert(decryptionEventsCollection, async, new BsonDocument("encrypted", ciphertext));
                            Aggregate(decryptionEventsCollection, async);

                            var reply = eventCapturer.Next().Should().BeOfType<CommandSucceededEvent>().Which.Reply;
                            eventCapturer.Any().Should().BeFalse();
                            reply["cursor"]["firstBatch"].AsBsonArray.Single()["encrypted"].AsBsonBinaryData.SubType.Should().Be(BsonBinarySubType.Encrypted);
                        }
                        break;
                    default: throw new Exception($"Unexpected test case {testCase}.");
                }
            }

            BsonDocument Aggregate(IMongoCollection<BsonDocument> collection, bool async)
            {
                var matchAggregatePipeline = new EmptyPipelineDefinition<BsonDocument>().Match(FilterDefinition<BsonDocument>.Empty);
                return async
                    ? collection.AggregateAsync(matchAggregatePipeline).GetAwaiter().GetResult().Single()
                    : collection.Aggregate(matchAggregatePipeline).Single();
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void ExplicitEncryptionTest(
            [Range(1, 5)] int testCase,
            [Values(false, true)] bool async)
        {
            // CSHARP-4606: Skip all fle2v2 tests on Mac until https://jira.mongodb.org/browse/SERVER-69563 propagates to EG Macs.
            RequirePlatform.Check().SkipWhen(SupportedOperatingSystem.MacOS);

            RequireServer.Check().Supports(Feature.Csfle2QEv2).ClusterTypes(ClusterType.ReplicaSet, ClusterType.Sharded, ClusterType.LoadBalanced);

            var encryptedFields = JsonFileReader.Instance.Documents["etc.data.encryptedFields.json"];
            var key1Document = JsonFileReader.Instance.Documents["etc.data.keys.key1-document.json"];
            var key1Id = key1Document["_id"].AsGuid;
            var explicitCollectionNamespace = CollectionNamespace.FromFullName("db.explicit_encryption");
            var value = "encrypted indexed value";

            using (var client = ConfigureClient(clearCollections: true, mainCollectionNamespace: explicitCollectionNamespace, encryptedFields: encryptedFields))
            {
                CreateCollection(client, explicitCollectionNamespace, encryptedFields: encryptedFields);
                CreateCollection(client, __keyVaultCollectionNamespace);
                var keyVaultCollection = GetCollection(client, __keyVaultCollectionNamespace);
                Insert(keyVaultCollection, async, key1Document);

                using (var keyVaultClient = CreateMongoClient())
                using (var clientEncryption = ConfigureClientEncryption(keyVaultClient, kmsProviderFilter: "local"))
                using (var encryptedClient = ConfigureClientEncrypted(kmsProviderFilter: "local", autoEncryptionOptionsConfigurator: (options) => options.With(bypassQueryAnalysis: true)))
                {
                    var explicitCollection = GetCollection(encryptedClient, explicitCollectionNamespace);

                    RunTestCase(explicitCollection, clientEncryption, testCase);
                }
            }

            void RunTestCase(IMongoCollection<BsonDocument> explicitCollectionFromEncryptedClient, ClientEncryption clientEncryption, int testCase)
            {
                switch (testCase)
                {
                    case 1: // Case 1: can insert encrypted indexed and find
                        {
                            var encryptionOptions = new EncryptOptions(algorithm: EncryptionAlgorithm.Indexed.ToString(), keyId: key1Id, contentionFactor: 0);
                            var encryptedValue = ExplicitEncrypt(clientEncryption, encryptionOptions, value, async);

                            var insertPayload = new BsonDocument("encryptedIndexed", encryptedValue);
                            Insert(explicitCollectionFromEncryptedClient, async, insertPayload);

                            encryptionOptions = new EncryptOptions(algorithm: EncryptionAlgorithm.Indexed.ToString(), keyId: key1Id, queryType: "equality", contentionFactor: 0);
                            encryptedValue = ExplicitEncrypt(clientEncryption, encryptionOptions, value, async);

                            var findPayload = new BsonDocument("encryptedIndexed", encryptedValue);
                            var result = Find(explicitCollectionFromEncryptedClient, findPayload, async).Single();
                            result.Elements.Should().Contain(new BsonElement("encryptedIndexed", value));
                        }
                        break;
                    case 2: // Case 2: can insert encrypted indexed and find with non-zero contention
                        {
                            var encryptionOptions = new EncryptOptions(algorithm: EncryptionAlgorithm.Indexed.ToString(), keyId: key1Id, contentionFactor: 10);

                            BsonBinaryData encryptedValue;
                            for (int i = 0; i < 10; i++)
                            {
                                encryptedValue = ExplicitEncrypt(clientEncryption, encryptionOptions, value, async);

                                var insertPayload = new BsonDocument("encryptedIndexed", encryptedValue);
                                Insert(explicitCollectionFromEncryptedClient, async, insertPayload);
                            }

                            // 1
                            encryptionOptions = new EncryptOptions(algorithm: EncryptionAlgorithm.Indexed.ToString(), keyId: key1Id, queryType: "equality", contentionFactor: 0);
                            encryptedValue = ExplicitEncrypt(clientEncryption, encryptionOptions, value, async);

                            var findPayload = new BsonDocument("encryptedIndexed", encryptedValue);
                            var result = Find(explicitCollectionFromEncryptedClient, findPayload, async).ToList();
                            // Assert less than 10 documents are returned. 0 documents may be returned
                            result.Count.Should().BeLessThan(10);
                            foreach (var doc in result)
                            {
                                doc.Elements.Should().Contain(new BsonElement("encryptedIndexed", value));
                            }

                            // 2
                            encryptionOptions = new EncryptOptions(algorithm: EncryptionAlgorithm.Indexed.ToString(), keyId: key1Id, queryType: "equality", contentionFactor: 10);
                            encryptedValue = ExplicitEncrypt(clientEncryption, encryptionOptions, value, async);

                            var findPayload2 = new BsonDocument("encryptedIndexed", encryptedValue);
                            result = Find(explicitCollectionFromEncryptedClient, findPayload2, async).ToList();
                            // Assert 10 documents are returned
                            result.Count.Should().Be(10);
                            foreach (var doc in result)
                            {
                                doc.Elements.Should().Contain(new BsonElement("encryptedIndexed", value));
                            }
                        }
                        break;
                    case 3: // Case 3: can insert encrypted unindexed
                        {
                            var encryptionOptions = new EncryptOptions(algorithm: EncryptionAlgorithm.Unindexed.ToString(), keyId: key1Id);
                            var encryptedValue = ExplicitEncrypt(clientEncryption, encryptionOptions, value, async);

                            var insertPayload = new BsonDocument { { "_id", 1 }, { "encryptedIndexed", encryptedValue } };
                            Insert(explicitCollectionFromEncryptedClient, async, insertPayload);

                            var findPayload = new BsonDocument("_id", 1);
                            var result = Find(explicitCollectionFromEncryptedClient, findPayload, async).Single();
                            result.Elements.Should().Contain(new BsonElement("encryptedIndexed", value));
                        }
                        break;
                    case 4: // Case 4: can insert encrypted unindexed
                        {
                            var encryptionOptions = new EncryptOptions(algorithm: EncryptionAlgorithm.Indexed.ToString(), keyId: key1Id, contentionFactor: 0);
                            var payload = ExplicitEncrypt(clientEncryption, encryptionOptions, value, async);

                            var decrypted = ExplicitDecrypt(clientEncryption, payload, async);

                            decrypted.Should().Be(BsonValue.Create(value));
                        }
                        break;
                    case 5: // Case 5: can roundtrip encrypted unindexed
                        {
                            var encryptionOptions = new EncryptOptions(algorithm: EncryptionAlgorithm.Unindexed.ToString(), keyId: key1Id);
                            var payload = ExplicitEncrypt(clientEncryption, encryptionOptions, value, async);

                            var decrypted = ExplicitDecrypt(clientEncryption, payload, async);

                            decrypted.Should().Be(BsonValue.Create(value));
                        }
                        break;
                    default: throw new Exception($"Unexpected test case {testCase}.");
                }
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void ExternalKeyVaultTest(
            [Values(false, true)] bool withExternalKeyVault,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            IMongoClient externalKeyVaultClient = null;
            if (withExternalKeyVault)
            {
                var externalKeyVaultClientSettings = DriverTestConfiguration.GetClientSettings().Clone();
                externalKeyVaultClientSettings.Credential = MongoCredential.FromComponents(null, null, "fake-user", "fake-pwd");
                externalKeyVaultClient = new MongoClient(externalKeyVaultClientSettings);
            }

            var clientEncryptedSchema = new BsonDocument("db.coll", JsonFileReader.Instance.Documents["external.external-schema.json"]);
            using (var client = ConfigureClient())
            using (var clientEncrypted = ConfigureClientEncrypted(clientEncryptedSchema, externalKeyVaultClient: externalKeyVaultClient, kmsProviderFilter: "local"))
            using (var clientEncryption = ConfigureClientEncryption(clientEncrypted, kmsProviderFilter: "local"))
            {
                var datakeys = GetCollection(client, __keyVaultCollectionNamespace);
                var externalKey = JsonFileReader.Instance.Documents["external.external-key.json"];
                Insert(datakeys, async, externalKey);

                var coll = GetCollection(clientEncrypted, __collCollectionNamespace);
                var exception = Record.Exception(() => Insert(coll, async, new BsonDocument("encrypted", "test")));
                if (withExternalKeyVault)
                {
                    AssertInnerEncryptionException<MongoAuthenticationException>(exception);
                }
                else
                {
                    exception.Should().BeNull();
                }

                var encryptionOptions = new EncryptOptions(
                    algorithm: EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic.ToString(),
                    keyId: GuidConverter.FromBytes(Convert.FromBase64String("LOCALAAAAAAAAAAAAAAAAA=="), GuidRepresentation.Standard));
                exception = Record.Exception(() => ExplicitEncrypt(clientEncryption, encryptionOptions, "test", async));
                if (withExternalKeyVault)
                {
                    AssertInnerEncryptionException<MongoAuthenticationException>(exception);
                }
                else
                {
                    exception.Should().BeNull();
                }
            }
        }

        [Theory]
        [ParameterAttributeData]
        public async Task KmsRetryTest(
            [Values("aws", "azure", "gcp")] string kmsProvider,
            [Values("network", "http")] string failureType,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);
            RequireEnvironment.Check().EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: true);

            const string endpoint = "127.0.0.1:9003";

            var masterKey = kmsProvider switch
            {
                "aws" => new BsonDocument
                {
                    { "region", "foo" },
                    { "key", "bar" },
                    { "endpoint", $"{endpoint}" }
                },
                "azure" => new BsonDocument
                {
                    { "keyVaultEndpoint", $"{endpoint}" },
                    { "keyName", "foo" },
                },
                "gcp" => new BsonDocument
                {
                    { "projectId", "foo" },
                    { "location", "bar" },
                    { "keyRing", "baz" },
                    { "keyName", "qux" },
                    { "endpoint", $"{endpoint}" }
                },
                _ => throw new ArgumentException(nameof(kmsProvider))
            };

            await ResetServer();

            using var clientEncrypted = ConfigureClientEncrypted();
            using var clientEncryption = ConfigureClientEncryption(
                clientEncrypted,
                kmsProviderFilter: kmsProvider,
                kmsProviderConfigurator: KmsProviderEndpointConfigurator
            );

            var dataKeyOptions = CreateDataKeyOptions(kmsProvider, customMasterKey: masterKey);

            await SetFailure(failureType, 1);

            Guid dataKey = default;
            Exception ex;
            if (async)
            {
                ex = await Record.ExceptionAsync(async () => dataKey = await clientEncryption
                    .CreateDataKeyAsync(kmsProvider, dataKeyOptions, CancellationToken.None));
            }
            else
            {
                ex = Record.Exception(() => dataKey = clientEncryption
                    .CreateDataKey(kmsProvider, dataKeyOptions, CancellationToken.None));
            }
            ex.Should().BeNull();

            await SetFailure(failureType, 1);

            Exception ex2;
            if (async)
            {
                ex2 = await Record.ExceptionAsync(async () => await clientEncryption.EncryptAsync(new BsonInt32(123),
                    new EncryptOptions("AEAD_AES_256_CBC_HMAC_SHA_512-Deterministic", keyId: dataKey)));
            }
            else
            {
                ex2 = Record.Exception(() => clientEncryption.Encrypt(new BsonInt32(123),
                    new EncryptOptions("AEAD_AES_256_CBC_HMAC_SHA_512-Deterministic", keyId: dataKey)));
            }
            ex2.Should().BeNull();

            if (failureType == "network")
            {
                await SetFailure("network", 4);

                Exception ex3;
                if (async)
                {
                    ex3 = await Record.ExceptionAsync(async () => dataKey = await clientEncryption
                        .CreateDataKeyAsync(kmsProvider, dataKeyOptions, CancellationToken.None));
                }
                else
                {
                    ex3 = Record.Exception(() => dataKey = clientEncryption
                        .CreateDataKey(kmsProvider, dataKeyOptions, CancellationToken.None));
                }
                ex3.Should().NotBeNull();
            }

            return;

            void KmsProviderEndpointConfigurator(string kmsProviderName, Dictionary<string, object> kmsOptions)
            {
                switch (kmsProviderName)
                {
                    case "aws":
                        break;
                    case "azure":
                        kmsOptions.Add("identityPlatformEndpoint", endpoint);
                        break;
                    case "gcp":
                        kmsOptions.Add("endpoint", endpoint);
                        break;
                    default:
                        throw new Exception($"Unexpected kmsProvider {endpoint}.");
                }
            }

            async Task SetFailure(string failure, int count)
            {
                using var client = new HttpClient();
                var jsonData = new { count }.ToJson();
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var uri = new Uri($"https://{endpoint}/set_failpoint/{failure}");
                var response = await client.PostAsync(uri, content);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Error while setting failure!");
                }
            }

            async Task ResetServer()
            {
                using var client = new HttpClient();
                var uri = new Uri($"https://{endpoint}/reset");
                var response = await client.PostAsync(uri, null);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Error while resetting!");
                }
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void KmsTlsOptionsTest(
            [Values("aws", "aws:name1", "azure", "azure:name1", "gcp", "gcp:name1", "kmip", "kmip:name1")] string kmsProvider,
            [Values(CertificateType.TlsWithoutClientCert, CertificateType.TlsWithClientCert, CertificateType.Expired, CertificateType.InvalidHostName)] CertificateType certificateType,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);
            RequireEnvironment.Check().EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: true);

            bool? isCertificateExpired = null, isInvalidHost = null; // will be assigned inside TestRelatedClientEncryptionOptionsConfigurator

            using (var clientEncrypted = ConfigureClientEncrypted())
            using (var clientEncryption = ConfigureClientEncryption(
                clientEncrypted,
                kmsProviderConfigurator: KmsProviderEndpointConfigurator,
                allowClientCertificateFunc: (kmsName) => kmsName == kmsProvider && certificateType == CertificateType.TlsWithClientCert,
                clientEncryptionOptionsConfigurator: TestRelatedClientEncryptionOptionsConfigurator,
                kmsProviderFilter: kmsProvider))
            {
                var dataKeyOptions = CreateDataKeyOptions(
                    kmsProvider: kmsProvider,
                    customMasterKey: kmsProvider switch
                    {
                        "aws" or "aws:name1" => new BsonDocument
                        {
                            { "region", "us-east-1" },
                            { "key", "arn:aws:kms:us-east-1:579766882180:key/89fcc2c4-08b0-4bd9-9f25-e30687b580d0" },
                            { "endpoint", GetMockedKmsEndpoint() }
                        },
                        "azure" or "azure:name1" => new BsonDocument
                        {
                            { "keyVaultEndpoint", "doesnotexist.invalid" },
                            { "keyName", "foo" }
                        },
                        "gcp" or "gcp:name1" => new BsonDocument
                        {
                            { "projectId", "foo" },
                            { "location", "bar" },
                            { "keyRing", "baz" },
                            { "keyName", "foo" }
                        },
                        "kmip" or "kmip:name1" => new BsonDocument(), // empty doc
                        _ => throw new Exception($"Unexpected kmsProvider {kmsProvider}."),
                    });

                var exception = Record.Exception(() => CreateDataKey(clientEncryption, kmsProvider, dataKeyOptions, async));
                AssertException(exception);
            }

            void AssertException(Exception exception)
            {
#if NET6_0_OR_GREATER
                const string invalidCertificateError = "The remote certificate was rejected by the provided RemoteCertificateValidationCallback.";
#else
                const string invalidCertificateError = "The remote certificate is invalid according to the validation procedure.";
#endif

                var currentOperatingSystem = OperatingSystemHelper.CurrentOperatingSystem;
                switch (kmsProvider)
                {
                    case "aws" or "aws:name1":
                        {
                            switch (certificateType)
                            {
                                case CertificateType.TlsWithoutClientCert:
                                    AssertCertificate(isExpired: null, invalidHost: null);
                                    // Expect an error indicating TLS handshake failed.
                                    switch (currentOperatingSystem)
                                    {
                                        case OperatingSystemPlatform.Windows:
                                            AssertTlsWithoutClientCertOnWindows(exception);
                                            break;
                                        case OperatingSystemPlatform.Linux:
                                            AssertTlsWithoutClientCertOnLinux(exception);
                                            break;
                                        case OperatingSystemPlatform.MacOS:
                                            AssertInnerEncryptionException(
                                                exception,
                                                Type.GetType("Interop+AppleCrypto+SslException, System.Net.Security", throwOnError: true),
                                                ex => ex.Message.Should().Contain("handshake failure"));
                                            break;
                                        default: throw new Exception($"Unsupported OS {currentOperatingSystem}.");
                                    }
                                    break;
                                case CertificateType.TlsWithClientCert:
                                    AssertCertificate(isExpired: null, invalidHost: null);
                                    // Expect an error from libmongocrypt with a message containing the string: "parse
                                    // error". This implies TLS handshake succeeded.
                                    AssertInnerEncryptionException<CryptException>(exception, "Got parse error");
                                    break;
                                case CertificateType.Expired:
                                    AssertCertificate(isExpired: true, invalidHost: false);
                                    // Expect an error indicating TLS handshake failed due to an expired certificate.
                                    AssertInnerEncryptionException<AuthenticationException>(exception, invalidCertificateError);
                                    break;
                                case CertificateType.InvalidHostName:
                                    AssertCertificate(isExpired: false, invalidHost: true);
                                    // Expect an error indicating TLS handshake failed due to an invalid hostname.
                                    AssertInnerEncryptionException<AuthenticationException>(exception, invalidCertificateError);
                                    break;
                                default: throw new Exception($"Unexpected certificate type {certificateType} for {kmsProvider}.");
                            }
                        }
                        break;
                    case "azure" or "azure:name1":
                        switch (certificateType)
                        {
                            case CertificateType.TlsWithoutClientCert:
                                AssertCertificate(isExpired: null, invalidHost: null);
                                // Expect an error indicating TLS handshake failed.
                                switch (currentOperatingSystem)
                                {
                                    case OperatingSystemPlatform.Windows:
                                        AssertTlsWithoutClientCertOnWindows(exception);
                                        break;
                                    case OperatingSystemPlatform.Linux:
                                        AssertTlsWithoutClientCertOnLinux(exception);
                                        break;
                                    case OperatingSystemPlatform.MacOS:
                                        AssertInnerEncryptionException(
                                            exception,
                                            Type.GetType("Interop+AppleCrypto+SslException, System.Net.Security", throwOnError: true),
                                            ex => ex.Message.Should().Contain("handshake failure"));
                                        break;
                                    default: throw new Exception($"Unsupported OS {currentOperatingSystem}.");
                                }
                                break;
                            case CertificateType.TlsWithClientCert:
                                AssertCertificate(isExpired: null, invalidHost: null);
                                // Expect an HTTP 404 error from libmongocrypt. This implies TLS handshake succeeded.
                                AssertInnerEncryptionException<CryptException>(exception, "404");
                                break;
                            case CertificateType.Expired:
                                AssertCertificate(isExpired: true, invalidHost: false);
                                // Expect an error indicating TLS handshake failed due to an expired certificate.
                                AssertInnerEncryptionException<AuthenticationException>(exception, invalidCertificateError);
                                break;
                            case CertificateType.InvalidHostName:
                                AssertCertificate(isExpired: false, invalidHost: true);
                                // Expect an error indicating TLS handshake failed due to an invalid hostname.
                                AssertInnerEncryptionException<AuthenticationException>(exception, invalidCertificateError);
                                break;
                            default: throw new Exception($"Unexpected certificate type {certificateType} for {kmsProvider}.");
                        }
                        break;
                    case "gcp" or "gcp:name1":
                        switch (certificateType)
                        {
                            case CertificateType.TlsWithoutClientCert:
                                AssertCertificate(isExpired: null, invalidHost: null);
                                // Expect an error indicating TLS handshake failed.
                                switch (currentOperatingSystem)
                                {
                                    case OperatingSystemPlatform.Windows:
                                        AssertTlsWithoutClientCertOnWindows(exception);
                                        break;
                                    case OperatingSystemPlatform.Linux:
                                        AssertTlsWithoutClientCertOnLinux(exception);
                                        break;
                                    case OperatingSystemPlatform.MacOS:
                                        AssertInnerEncryptionException(
                                            exception,
                                            Type.GetType("Interop+AppleCrypto+SslException, System.Net.Security", throwOnError: true),
                                            ex => ex.Message.Should().Contain("handshake failure"));
                                        break;
                                    default: throw new Exception($"Unsupported OS {currentOperatingSystem}.");
                                }
                                break;
                            case CertificateType.TlsWithClientCert:
                                AssertCertificate(isExpired: null, invalidHost: null);
                                // Expect an HTTP 404 error from libmongocrypt. This implies TLS handshake succeeded.
                                AssertInnerEncryptionException<CryptException>(exception, "404");
                                break;
                            case CertificateType.Expired:
                                AssertCertificate(isExpired: true, invalidHost: false);
                                // Expect an error indicating TLS handshake failed due to an expired certificate.
                                AssertInnerEncryptionException<AuthenticationException>(exception, invalidCertificateError);
                                break;
                            case CertificateType.InvalidHostName:
                                AssertCertificate(isExpired: false, invalidHost: true);
                                // Expect an error indicating TLS handshake failed due to an invalid hostname.
                                AssertInnerEncryptionException<AuthenticationException>(exception, invalidCertificateError);
                                break;
                            default: throw new Exception($"Unexpected certificate type {certificateType} for {kmsProvider}.");
                        }
                        break;
                    case "kmip" or "kmip:name1":
                        switch (certificateType)
                        {
                            case CertificateType.TlsWithoutClientCert:
                                AssertCertificate(isExpired: null, invalidHost: null);
                                // Expect an error indicating TLS handshake failed.
                                switch (currentOperatingSystem)
                                {
                                    case OperatingSystemPlatform.Windows:
                                        AssertTlsWithoutClientCertOnWindows(exception);
                                        break;
                                    case OperatingSystemPlatform.Linux:
                                        AssertTlsWithoutClientCertOnLinux(exception);
                                        break;
                                    case OperatingSystemPlatform.MacOS:
                                        AssertInnerEncryptionException(
                                            exception,
                                            Type.GetType("Interop+AppleCrypto+SslException, System.Net.Security", throwOnError: true),
                                            ex => ex.Message.Should().Contain("handshake failure"));
                                        break;
                                    default: throw new Exception($"Unsupported OS {currentOperatingSystem}.");
                                }
                                break;
                            case CertificateType.TlsWithClientCert:
                                AssertCertificate(isExpired: null, invalidHost: null);
                                exception.Should().BeNull();
                                break;
                            case CertificateType.Expired:
                                AssertCertificate(isExpired: true, invalidHost: false);
                                // Expect an error indicating TLS handshake failed due to an expired certificate.
                                AssertInnerEncryptionException<AuthenticationException>(exception, invalidCertificateError);
                                break;
                            case CertificateType.InvalidHostName:
                                AssertCertificate(isExpired: false, invalidHost: true);
                                // Expect an error indicating TLS handshake failed due to an invalid hostname.
                                AssertInnerEncryptionException<AuthenticationException>(exception, invalidCertificateError);
                                break;
                            default: throw new Exception($"Unexpected certificate type {certificateType} for {kmsProvider}.");
                        }
                        break;
                    default: throw new Exception($"Not supported client certificate type {certificateType}.");
                }
            }

            void AssertCertificate(bool? isExpired, bool? invalidHost)
            {
                isCertificateExpired.Should().Be(isExpired);
                isInvalidHost.Should().Be(invalidHost);
            }

            void AssertTlsWithoutClientCertOnLinux(Exception exception)
            {
                try
                {
                    AssertInnerEncryptionException(
                        exception,
                        Type.GetType("Interop+OpenSsl+SslException, System.Net.Security", throwOnError: true),
                        ex => ex.Message.Should().BeOneOf("SSL Handshake failed with OpenSSL error - SSL_ERROR_SSL.", "Decrypt failed with OpenSSL error - SSL_ERROR_SSL."));
                }
                catch (XunitException)
                {
                    // With Tls1.3, there is no report of a failed handshake if the client certificate verification fails
                    // since the client receives a 'Finished' message from the server before sending its certificate, it assumes
                    // authentication and we will not know if there was an error until we next read/write from the server.
                    AssertInnerEncryptionException<SocketException>(exception, "Connection reset by peer");
                }
            }

            void AssertTlsWithoutClientCertOnWindows(Exception exception)
            {
                try
                {
                    AssertInnerEncryptionException<System.ComponentModel.Win32Exception>(exception,"The message received was unexpected or badly formatted");
                }
                catch (XunitException) // assertation failed
                {
                    // Sometimes the mock server triggers SocketError.ConnectionReset (10054) on windows instead the expected exception.
                    // It looks like a test env issue, a similar behavior presents in other drivers, so we rely on the same check on different OSs
                    AssertInnerEncryptionException<SocketException>(exception, "An existing connection was forcibly closed by the remote host");
                }
            }

            void KmsProviderEndpointConfigurator(string kmsProviderName, Dictionary<string, object> kmsOptions)
            {
                string endpoint = GetMockedKmsEndpoint();

                switch (kmsProviderName)
                {
                    case "local" or "local:name1" or "local:name2":
                        // not related to this test, do nothing
                        break;
                    case "aws" or "aws:name1" or "aws:name2":
                        // do nothing since aws cannot configure endpoint on kms provider level
                        break;
                    case "azure" or "azure:name1":
                        kmsOptions.Add("identityPlatformEndpoint", endpoint);
                        break;
                    case "gcp" or "gcp:name1":
                        kmsOptions.Add("endpoint", endpoint);
                        break;
                    case "kmip" or "kmip:name1":
                        AddOrReplace(kmsOptions, "endpoint", endpoint);
                        break;
                    default:
                        throw new Exception($"Unexpected kmsProvider {kmsProvider}.");
                }
            }

            string GetMockedKmsEndpoint() => certificateType switch
            {
                CertificateType.Expired => "127.0.0.1:9000",
                CertificateType.InvalidHostName => "127.0.0.1:9001",
                CertificateType.TlsWithClientCert or CertificateType.TlsWithoutClientCert => !kmsProvider.StartsWith("kmip") ? "127.0.0.1:9002" : "127.0.0.1:5698",
                _ => throw new Exception($"Not supported client certificate type {certificateType}."),
            };

            void TestRelatedClientEncryptionOptionsConfigurator(ClientEncryptionOptions clientEncryptionOptions) // needs only for asserting reasons
            {
                var tlsOptions = new Dictionary<string, SslSettings>((IDictionary<string, SslSettings>)clientEncryptionOptions.TlsOptions);
                if (!tlsOptions.ContainsKey(kmsProvider))
                {
                    tlsOptions.Add(kmsProvider, new SslSettings()); // configure it regardless global tls configuration to be able to validate certificate
                }

                tlsOptions[kmsProvider].ServerCertificateValidationCallback = new RemoteCertificateValidationCallback((subject, certificate, chain, policyErrors) =>
                {
                    if (policyErrors == SslPolicyErrors.None)
                    {
                        // certificate is valid
                        return true;
                    }

                    var x509certificate2 = (X509Certificate2)certificate;
                    isCertificateExpired = x509certificate2.NotAfter < DateTime.UtcNow;
                    isInvalidHost = policyErrors == SslPolicyErrors.RemoteCertificateNameMismatch && certificate.Subject.Contains("wronghost.com");

                    Ensure.That(isCertificateExpired.GetValueOrDefault() || isInvalidHost.GetValueOrDefault(), $"Unexpected certificate issue detected for cert: {x509certificate2} and policyErrors: {policyErrors}.");

                    return false;
                });
                clientEncryptionOptions._tlsOptions(tlsOptions); // avoid validation on serverCertificateValidationCallback
            }
        }

        [Trait("Category", "CsfleAZUREKMS")]
        [Trait("Category", "CsfleGCPKMS")]
        [Fact]
        public void OnDemandCredentialsTestWithNamed()
        {
            // This test specifically verifies part of the CSFLE specification that
            // KMS providers that include a name do not support automatic credentials.

            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            var kmsProvider = "aws:name1";

            var exception = Record.Exception(() =>
            {
                using var client = ConfigureClient();
                using var clientEncryption = ConfigureClientEncryption(client, kmsDocument: new BsonDocument(kmsProvider, new BsonDocument()));
            });

            exception?.Message.Should().Contain("On-demand credentials are not supported for named KMS providers");
        }

        [Trait("Category", "CsfleAZUREKMS")]
        [Trait("Category", "CsfleGCPKMS")]
        [Theory]
        [ParameterAttributeData]
        public void OnDemandCredentialsTest(
            [Values("aws", "azure", "gcp")] string kmsProvider,
            [Values(false, true)] bool expectedEnvironment,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            EnsureEnvironmentConfigured(out var masterKey);

            using (var client = ConfigureClient(clearCollections: true))
            using (var clientEncryption = ConfigureClientEncryption(client, kmsDocument: new BsonDocument(kmsProvider, new BsonDocument())))
            {
                var datakeyOptions = CreateDataKeyOptions(kmsProvider, customMasterKey: masterKey);
                var ex = Record.Exception(() => CreateDataKey(clientEncryption, kmsProvider, datakeyOptions, async));
                if (expectedEnvironment)
                {
                    // all expected env setup MUST be configured
                    ex.Should().BeNull();
                }
                else
                {
                    AssertException(ex);
                }

                void AssertException(Exception ex)
                {
                    var currentOperatingSystem = OperatingSystemHelper.CurrentOperatingSystem;
                    switch (kmsProvider)
                    {
                        // AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY must not be configured
                        case "aws":
                            {
                                try
                                {
                                    AssertInnerEncryptionException<AmazonServiceException>(ex, "Unable to get IAM security credentials from EC2 Instance Metadata Service.");
                                }
                                catch (XunitException)
                                {
                                    // In rare cases, the thrown error is "CryptException exception: AccessDeniedException". That means you don't have authorization to perform the requested action.
                                    // It more or less corresponds to the expected behavior here, but it's unclear why the same scenario triggers different exceptions.
                                    // However, it looks harmless to slightly update the test assertion to avoid assertion failures on EG.
                                    AssertInnerEncryptionException<CryptException>(ex, "\"__type\":\"AccessDeniedException\"");
                                }
                            }
                            break;
                        case "azure":
                            {
                                switch (currentOperatingSystem)
                                {
                                    case OperatingSystemPlatform.Windows:
                                    case OperatingSystemPlatform.Linux:
                                        {
                                            AssertInnerEncryptionException<MongoClientException>(ex, "Failed to acquire IMDS access token.");
                                        }
                                        break;
                                    case OperatingSystemPlatform.MacOS:
                                        {
                                            try
                                            {
                                                AssertInnerEncryptionException<TaskCanceledException>(ex);
                                            }
                                            catch (XunitException)
                                            {
                                                AssertInnerEncryptionException<MongoClientException>(ex, "Failed to acquire IMDS access token.");
                                            }
                                        }
                                        break;
                                    default: throw new Exception($"Unexpected OS: {currentOperatingSystem}");
                                }
                            }
                            break;
                        case "gcp":
                            {
                                AssertInnerEncryptionException<MongoClientException>(ex, "Failed to acquire gce metadata credentials.");
                            }
                            break;
                        default: throw new Exception($"Unexpected kms provider: {kmsProvider}.");
                    }
                }
            }

            void EnsureEnvironmentConfigured(out BsonDocument customMasterKey)
            {
                customMasterKey = null;
                var requireEnvironmentCheck = RequireEnvironment.Check();
                switch (kmsProvider)
                {
                    case "aws":
                        {
                            requireEnvironmentCheck.EnvironmentVariable("AWS_ACCESS_KEY_ID", isDefined: expectedEnvironment);
                            // mocked env doesn't configure aws_temp credentials with AWS_ACCESS_KEY_ID
                            requireEnvironmentCheck.EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: !expectedEnvironment);
                        }
                        break;
                    case "azure":
                        {
                            if (Environment.GetEnvironmentVariable("CSFLE_AZURE_KMS_TESTS_ENABLED") != null)
                            {
                                // azure env
                                if (!expectedEnvironment)
                                {
                                    throw new SkipException("Test skipped, because current env should not be Azure.");
                                }
                            }
                            else
                            {
                                // It can work everywhere, but limit running these tests here since a single test run can take up to 10 seconds
                                requireEnvironmentCheck
                                    .EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: true)
                                    .EnvironmentVariable("CSFLE_AZURE_KMS_TESTS_ENABLED", isDefined: expectedEnvironment);
                            }

                            customMasterKey = new BsonDocument
                            {
                                { "keyVaultEndpoint", "https://drivers-2411-keyvault.vault.azure.net/" },
                                { "keyName", "drivers-2411-keyname" }
                            };
                        }
                        break;
                    case "gcp":
                        {
                            if (Environment.GetEnvironmentVariable("CSFLE_GCP_KMS_TESTS_ENABLED") != null)
                            {
                                // gcp env
                                if (!expectedEnvironment)
                                {
                                    throw new SkipException("Test skipped, because current env should not be GCP.");
                                }
                            }
                            else
                            {
                                // mocked env
                                // gcp mocked server fails on non windows env
                                RequirePlatform
                                    .Check()
                                    .SkipWhen(SupportedOperatingSystem.Linux)
                                    .SkipWhen(SupportedOperatingSystem.MacOS);

                                if (expectedEnvironment)
                                {
                                    requireEnvironmentCheck
                                        .EnvironmentVariable("CSFLE_GCP_KMS_TESTS_ENABLED", isDefined: false)
                                        // mocked env
                                        .EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: true)
                                        .EnvironmentVariable("GCE_METADATA_HOST", isDefined: expectedEnvironment)
                                        // required mock server
                                        .HostReachable((DnsEndPoint)EndPointHelper.Parse(Environment.GetEnvironmentVariable("GCE_METADATA_HOST")));
                                }
                                else
                                {
                                    requireEnvironmentCheck
                                        .EnvironmentVariable("CSFLE_GCP_KMS_TESTS_ENABLED", isDefined: false)
                                        .EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: false);
                                }
                            }
                        }
                        break;
                    default: throw new Exception($"Unexpected kms provider: {kmsProvider}.");
                }
            }
        }

        [Theory]
        [ParameterAttributeData]
        public async Task OnDemandAzureIMDSCredentialsUnitTest(
            [Range(1, 6)] int testCase,
            [Values(false, true)] bool async)
        {
            RequireEnvironment
                .Check()
                .EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED")
                .EnvironmentVariable("AZURE_IMDS_MOCK_ENDPOINT");

            switch (testCase)
            {
                case 1: // Case 1: Success
                    {
                        var result = await CreateTestCase(request => { });
                        result.AccessToken.Should().Be("magic-cookie");
                        // < 70 && >= 60 seconds
                        result.Expiration.Should().BeCloseTo(DateTime.UtcNow + TimeSpan.FromSeconds(65), (int)TimeSpan.FromSeconds(5).TotalMilliseconds);
                    }
                    break;
                case 2: // Case 2: Empty JSON
                    {
                        var exception = await Record.ExceptionAsync(() => CreateTestCase((request) => request.Headers.Add("X-MongoDB-HTTP-TestParams", "case=empty-json")));
                        exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Azure IMDS response must contain access_token.");
                    }
                    break;
                case 3: // Case 3: Bad JSON
                    {
                        var exception = await Record.ExceptionAsync(() => CreateTestCase((request) => request.Headers.Add("X-MongoDB-HTTP-TestParams", "case=bad-json")));
                        exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Azure IMDS response must be in Json format.");
                    }
                    break;
                case 4: // Case 4: HTTP 404
                    {
                        var exception = await Record.ExceptionAsync(() => CreateTestCase((request) => request.Headers.Add("X-MongoDB-HTTP-TestParams", "case=404")));
                        exception
                            .Should().BeOfType<MongoClientException>().Which.InnerException
                            .Should().BeOfType<HttpRequestException>().Which.Message
                            .Should().Be("Response status code does not indicate success: 404 (Not Found).");
                    }
                    break;
                case 5: // Case 5: HTTP 500
                    {
                        var exception = await Record.ExceptionAsync(() => CreateTestCase((request) => request.Headers.Add("X-MongoDB-HTTP-TestParams", "case=500")));
                        exception
                            .Should().BeOfType<MongoClientException>().Which.InnerException
                            .Should().BeOfType<HttpRequestException>().Which.Message
                            .Should().Be("Response status code does not indicate success: 500 (Internal Server Error).");
                    }
                    break;
                case 6: // Case 6: Slow Response
                    {
                        var exception = await Record.ExceptionAsync(() => CreateTestCase((request) => request.Headers.Add("X-MongoDB-HTTP-TestParams", "case=slow")));
                        exception
                            .Should().BeOfType<MongoClientException>().Which.InnerException
                            .Should().BeAssignableTo<OperationCanceledException>();
                    }
                    break;
                default: throw new Exception($"Unexpected test case: {testCase}.");
            }

            async Task<AzureCredentials> CreateTestCase(Action<HttpRequestMessage> modifyAction)
            {
                var httpClientWrapperWithModifiedRequest = CreateHttpClientWrapperWithModifiedRequest(modifyAction);
                var azureProvider = new AzureAuthenticationCredentialsProvider(httpClientWrapperWithModifiedRequest);
                return async
                    ? await azureProvider.CreateCredentialsFromExternalSourceAsync(default)
                    : azureProvider.CreateCredentialsFromExternalSource(default);
            }

            HttpClientWrapperWithModifiedRequest CreateHttpClientWrapperWithModifiedRequest(Action<HttpRequestMessage> modifyAction)
            {
                var imdsMockEndpoint = Environment.GetEnvironmentVariable("AZURE_IMDS_MOCK_ENDPOINT") ?? throw new Exception("AZURE_IMDS_MOCK_ENDPOINT must be configured.");
                var httpClientHelper = ExternalCredentialsAuthenticators.Instance.HttpClientWrapper;
                var withReplacedEndpoint = (HttpRequestMessage httpRequestMessage) =>
                {
                    modifyAction(httpRequestMessage);
                    var uriBuilder = new UriBuilder(httpRequestMessage.RequestUri);
                    var mockUri = new Uri($"http://{imdsMockEndpoint}");
                    uriBuilder.Scheme = mockUri.Scheme;
                    uriBuilder.Host = mockUri.Host;
                    uriBuilder.Port = mockUri.Port;
                    httpRequestMessage.RequestUri = uriBuilder.Uri;
                };
                return new HttpClientWrapperWithModifiedRequest(httpClientHelper, withReplacedEndpoint);
            }
        }

        [Theory]
        [ParameterAttributeData]
        public async Task RangeExplicitEncryptionTest(
            [Range(1, 8)] int testCase,
            // test case rangeType values correspond to keys used in test configuration files
            [Values("DecimalNoPrecision", "DecimalPrecision", "DoubleNoPrecision", "DoublePrecision", "Date", "Int", "Long")] string rangeType,
            [Values(false, false)] bool async)
        {
            // CSHARP-4606: Skip all fle2v2 tests on Mac until https://jira.mongodb.org/browse/SERVER-69563 propagates to EG Macs.
            RequirePlatform.Check().SkipWhen(SupportedOperatingSystem.MacOS);

            RequireServer.Check()
                .Supports(Feature.Csfle2QEv2RangeAlgorithm)
                .ClusterTypes(ClusterType.ReplicaSet, ClusterType.Sharded, ClusterType.LoadBalanced);

            if (rangeType == "DecimalNoPrecision")
            {
                // Tests for ``DecimalNoPrecision`` must only run against a replica set.
                // ``DecimalNoPrecision`` queries are expected to take a long time and may exceed the default mongos timeout.
                RequireServer.Check().ClusterTypes(ClusterType.ReplicaSet);
            }

            var encryptedFields = JsonFileReader.Instance.Documents[$"etc.data.range-encryptedFields-{rangeType}.json"];
            var key1Document = JsonFileReader.Instance.Documents["etc.data.keys.key1-document.json"];
            var key1Id = key1Document["_id"].AsGuid;
            var kmsProvider = "local";
            var encryptedKeyWithRangeSupportedType = $"encrypted{rangeType}";
            var value0 = GetValue(0, rangeType);
            var value6 = GetValue(6, rangeType);
            var value30 = GetValue(30, rangeType);
            var value200 = GetValue(200, rangeType);
            var value201 = GetValue(201, rangeType);

            var explicitEncryption = CollectionNamespace.FromFullName("db.explicit_encryption");
            var encryptOptions = WithRangeOptions(rangeType, new EncryptOptions(EncryptionAlgorithm.Range, contentionFactor: 0, keyId: key1Id));

            using (var keyVaultClient = ConfigureClient(clearCollections: true, mainCollectionNamespace: explicitEncryption, encryptedFields: encryptedFields))
            {
                var keyVaultCollection = GetCollection(keyVaultClient, __keyVaultCollectionNamespace);
                Insert(keyVaultCollection, async, key1Document);

                using (var clientEncryption = ConfigureClientEncryption(keyVaultClient, kmsProviderFilter: kmsProvider))
                using (var encryptedClient = ConfigureClientEncrypted(kmsProviderFilter: kmsProvider, bypassQueryAnalysis: true))
                {
                    var encrypted0 = ExplicitEncrypt(clientEncryption, encryptOptions, value0, async);
                    var encrypted6 = ExplicitEncrypt(clientEncryption, encryptOptions, value6, async);
                    var encrypted30 = ExplicitEncrypt(clientEncryption, encryptOptions, value30, async);
                    var encrypted200 = ExplicitEncrypt(clientEncryption, encryptOptions, value200, async);

                    CreateCollection(encryptedClient, explicitEncryption, encryptedFields: encryptedFields);
                    var encryptedCollection = GetCollection(encryptedClient, explicitEncryption);
                    // bulk insert is not supported
                    Insert(
                        encryptedCollection,
                        async,
                        new BsonDocument { { encryptedKeyWithRangeSupportedType, encrypted0 }, { "_id", 0 } });
                    Insert(
                        encryptedCollection,
                        async,
                        new BsonDocument { { encryptedKeyWithRangeSupportedType, encrypted6 }, { "_id", 1 } });
                    Insert(
                        encryptedCollection,
                        async,
                        new BsonDocument { { encryptedKeyWithRangeSupportedType, encrypted30 }, { "_id", 2 } });
                    Insert(
                        encryptedCollection,
                        async,
                        new BsonDocument { { encryptedKeyWithRangeSupportedType, encrypted200 }, { "_id", 3 } });

                    await RunTestCase(clientEncryption, encryptedCollection, testCase);
                }
            }

            EncryptOptions WithRangeOptions(string rangeType, EncryptOptions encryptionOptions)
            {
                var rangeOptions = rangeType switch
                {
                    "DecimalNoPrecision" => new RangeOptions(sparsity: 1, trimFactor: 1),
                    "DecimalPrecision" => new RangeOptions(
                        sparsity: 1,
                        trimFactor: 1,
                        precision: 2,
                        min: new BsonDecimal128(0),
                        max: new BsonDecimal128(200)),
                    "DoubleNoPrecision" => new RangeOptions(sparsity: 1, trimFactor: 1),
                    "DoublePrecision" => new RangeOptions(
                        sparsity: 1,
                        trimFactor: 1,
                        min: new BsonDouble(0),
                        max: new BsonDouble(200),
                        precision: 2),
                    "Date" => new RangeOptions(
                        sparsity: 1,
                        trimFactor: 1,
                        min: new BsonDateTime(0),
                        max: new BsonDateTime(200)),
                    "Int" => new RangeOptions(
                        sparsity: 1,
                        trimFactor: 1,
                        min: new BsonInt32(0),
                        max: new BsonInt32(200)),
                    "Long" => new RangeOptions(
                        sparsity: 1,
                        trimFactor: 1,
                        min: new BsonInt64(0),
                        max: new BsonInt64(200)),
                    _ => throw new Exception($"Unsupported rangeSupportedType {rangeType}.")
                };

                return encryptionOptions.With(rangeOptions: rangeOptions);
            }


            async Task RunTestCase(ClientEncryption clientEncryption, IMongoCollection<BsonDocument> encryptedCollection, int testCase)
            {
                switch (testCase)
                {
                    case 1: // can decrypt a payload
                        {
                            var insertPayload6 = ExplicitEncrypt(clientEncryption, encryptOptions, value6, async);
                            var decryptedValue = ExplicitDecrypt(clientEncryption, insertPayload6, async);
                            decryptedValue.Should().Be(value6); // asserts types too
                        }
                        break;
                    case 2: // can find encrypted range and return the maximum
                        {
                            var findPayload = await ExplicitEncryptExpression(
                                clientEncryption,
                                encryptOptions.With(queryType: "range"),
                                expression: BsonDocument.Parse(@$"
                                {{
                                    ""$and"" :
                                    [
                                        {{ {encryptedKeyWithRangeSupportedType} : {{ ""$gte"" : {value6.ToJson(writerSettings: new JsonWriterSettings { OutputMode = JsonOutputMode.Shell })} }} }},
                                        {{ {encryptedKeyWithRangeSupportedType} : {{ ""$lte"" : {value200.ToJson(writerSettings: new JsonWriterSettings { OutputMode = JsonOutputMode.Shell })} }} }}
                                    ]
                                }}"),
                                async);

                            var findResult = Find(encryptedCollection, findPayload, async).ToList().OrderBy((d) => d["_id"]).ToList();
                            findResult.Should().HaveCount(3);

                            findResult[0][encryptedKeyWithRangeSupportedType].Should().Be(value6);
                            findResult[1][encryptedKeyWithRangeSupportedType].Should().Be(value30);
                            findResult[2][encryptedKeyWithRangeSupportedType].Should().Be(value200);
                        }
                        break;
                    case 3: // can find encrypted range and return the minimum
                        {
                            var findPayload = await ExplicitEncryptExpression(
                                clientEncryption,
                                encryptOptions.With(queryType: "range"),
                                expression: BsonDocument.Parse(@$"
                                {{
                                    ""$and"" :
                                    [
                                        {{ {encryptedKeyWithRangeSupportedType} : {{ ""$gte"" : {value0.ToJson(writerSettings: new JsonWriterSettings { OutputMode = JsonOutputMode.Shell })} }} }},
                                        {{ {encryptedKeyWithRangeSupportedType} : {{ ""$lte"" : {value6.ToJson(writerSettings: new JsonWriterSettings { OutputMode = JsonOutputMode.Shell })} }} }}
                                    ]
                                }}"),
                                async);

                            var findResult = Find(encryptedCollection, findPayload, async).ToList().OrderBy((d) => d["_id"]).ToList();
                            findResult.Should().HaveCount(2);

                            findResult[0][encryptedKeyWithRangeSupportedType].Should().Be(value0);
                            findResult[1][encryptedKeyWithRangeSupportedType].Should().Be(value6);
                        }
                        break;
                    case 4: // can find encrypted range with an open range query
                        {
                            var findPayload = await ExplicitEncryptExpression(
                                clientEncryption,
                                encryptOptions.With(queryType: "range"),
                                expression: BsonDocument.Parse(@$"
                                {{
                                    ""$and"" :
                                    [
                                        {{ {encryptedKeyWithRangeSupportedType} : {{ ""$gt"" :  {value30.ToJson(writerSettings: new JsonWriterSettings { OutputMode = JsonOutputMode.Shell })} }} }}
                                    ]
                                }}"),
                                async);

                            var findResult = Find(encryptedCollection, findPayload, async).ToList().OrderBy((d) => d["_id"]).ToList();
                            findResult.Should().HaveCount(1);

                            findResult[0][encryptedKeyWithRangeSupportedType].Should().Be(value200);
                        }
                        break;
                    case 5: // can run an aggregation expression inside $expr
                        {
                            var findPayload = await ExplicitEncryptExpression(
                               clientEncryption,
                               encryptOptions.With(queryType: "range"),
                               expression: BsonDocument.Parse(@$"
                               {{
                                    ""$and"" :
                                    [
                                        {{ ""$lt"" : [ ""${encryptedKeyWithRangeSupportedType}"", {value30.ToJson(writerSettings: new JsonWriterSettings { OutputMode = JsonOutputMode.Shell })} ] }}
                                    ]
                               }}"),
                               async);

                            var findResult = Find(encryptedCollection, BsonDocument.Parse(@$"{{ ""$expr"" : {findPayload} }}"), async).ToList().OrderBy((d) => d["_id"]).ToList();
                            findResult.Should().HaveCount(2);

                            findResult[0][encryptedKeyWithRangeSupportedType].Should().Be(value0);
                            findResult[1][encryptedKeyWithRangeSupportedType].Should().Be(value6);
                        }
                        break;
                    case 6: // encrypting a document greater than the maximum errors
                        {
                            if (rangeType == "DoubleNoPrecision" || rangeType == "DecimalNoPrecision")
                            {
                                throw new SkipException("Skip it based on spec requirement.");
                            }

                            var exception = Record.Exception(() => ExplicitEncrypt(clientEncryption, encryptOptions, value201, async));
                            AssertInnerEncryptionException<CryptException>(exception, "Value must be greater than or equal to the minimum value and less than or equal to the maximum value");
                        }
                        break;
                    case 7: // encrypting a document of a different type errors
                        {
                            if (rangeType == "DoubleNoPrecision" || rangeType == "DecimalNoPrecision")
                            {
                                throw new SkipException("Skip it based on spec requirement.");
                            }

                            var exception = Record.Exception(() =>
                                Insert(
                                    encryptedCollection,
                                    async,
                                    // If the encrypted field is ``encryptedInt`` insert ``{ "encryptedInt": { "$numberDouble": "6" } }``.
                                    // Otherwise, insert ``{ "encrypted<Type>": { "$numberInt": "6" }``.
                                    new BsonDocument(encryptedKeyWithRangeSupportedType, rangeType == "Int" ? GetValue(6, "DoubleNoPrecision") : GetValue(6, "Int"))));
                            exception.Should().BeOfType<MongoBulkWriteException<BsonDocument>>().Which.Message.Should().Contain("Document failed validation");
                        }
                        break;
                    case 8: // setting precision errors if the type is not a double
                        {
                            if (rangeType == "DoubleNoPrecision" || rangeType == "DoublePrecision" || rangeType == "DecimalPrecision" || rangeType == "DecimalNoPrecision")
                            {
                                throw new SkipException("Skip it based on spec requirement.");
                            }

                            var exception = Record.Exception(() =>
                                ExplicitEncrypt(
                                    clientEncryption,
                                    encryptOptions.With(rangeOptions: new RangeOptions(sparsity: 1, trimFactor: 1, min: BsonValue.Create(0), max: BsonValue.Create(200), precision: 2)),
                                    value6,
                                    async));
                            AssertInnerEncryptionException<CryptException>(exception, "expected 'precision' to be set with double or decimal128 index, but got: INT32 min");
                        }
                        break;
                }
            }

            BsonValue GetValue(int value, string rangeSupportedType) => rangeSupportedType switch
            {
                "DecimalNoPrecision" => new BsonDecimal128(value),
                "DecimalPrecision" => new BsonDecimal128(value),
                "DoubleNoPrecision" => new BsonDouble(value),
                "DoublePrecision" => new BsonDouble(value),
                "Date" => new BsonDateTime(millisecondsSinceEpoch: value),
                "Int" => new BsonInt32(value),
                "Long" => new BsonInt64(value),
                _ => throw new ArgumentException($"Unsupported {nameof(rangeSupportedType)} {rangeSupportedType}.")
            };
        }

        [Theory]
        [ParameterAttributeData]
        public void RewrapTest(
            [Values("local", "aws", "azure", "gcp", "kmip")] string srcProvider,
            [Values("local", "aws", "azure", "gcp", "kmip")] string dstProvider,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.Csfle2);

            // The test description requires configuring all kmsProviders in setup, but leaving only related to the provided income arguments
            // to avoid restrictions on kmip mocking setup for unrelated to kmip tests
            var kmsProviderFilter = EncryptionTestHelper.CreateKmsProviderFilter(srcProvider, dstProvider);
            if (kmsProviderFilter.Contains("kmip"))
            {
                RequireEnvironment.Check().EnvironmentVariable("KMS_MOCK_SERVERS_ENABLED", isDefined: true);
            }

            const string value = "test";

            using (var client1 = ConfigureClient(clearCollections: true))
            using (var clientEncryption1 = ConfigureClientEncryption(client1, kmsProviderFilter: kmsProviderFilter))
            {
                var datakeyOptions = CreateDataKeyOptions(srcProvider);
                var keyID = CreateDataKey(clientEncryption1, srcProvider, datakeyOptions, async);
                var ciphertext = ExplicitEncrypt(clientEncryption1, new EncryptOptions(keyId: keyID, algorithm: EncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA_512_Deterministic), value, async);

                using (var client2 = ConfigureClient(clearCollections: false))
                using (var clientEncryption2 = ConfigureClientEncryption(client2, kmsProviderFilter: kmsProviderFilter))
                {
                    var rewrapManyDataKeyOptions = CreateRewrapManyDataKeyOptions(dstProvider);
                    var result = RewrapManyDataKey(clientEncryption2, rewrapManyDataKeyOptions, async);
                    result.BulkWriteResult.ModifiedCount.Should().Be(1);

                    var decrypted = ExplicitDecrypt(clientEncryption1, ciphertext, async);
                    decrypted.Should().Be(BsonValue.Create(value));

                    decrypted = ExplicitDecrypt(clientEncryption2, ciphertext, async);
                    decrypted.Should().Be(BsonValue.Create(value));
                }
            }
        }

        [Fact]
        public void RewrapManyDataKeyOptions_ctor_should_validate_provider_is_set()
        {
            // rewrap prose test case 2
            var exception = Record.Exception(() => new RewrapManyDataKeyOptions(null, new BsonDocument()));
            exception.Should().BeOfType<ArgumentNullException>().Subject.ParamName.Should().Be("provider");

            exception = Record.Exception(() => new RewrapManyDataKeyOptions("", new BsonDocument()));
            exception.Should().BeOfType<ArgumentException>().Subject.ParamName.Should().Be("provider");
        }

        [Fact]
        public void RewrapManyDataKeyOptions_with_should_validate_provider_is_set()
        {
            // rewrap prose test case 2
            var subject = new RewrapManyDataKeyOptions("provider", new BsonDocument());
            var exception = Record.Exception(() => subject.With(provider: null));
            exception.Should().BeOfType<ArgumentNullException>().Subject.ParamName.Should().Be("provider");

            exception = Record.Exception(() => subject.With(provider: ""));
            exception.Should().BeOfType<ArgumentException>().Subject.ParamName.Should().Be("provider");
        }

        // 25. Test $lookup (cases 1-8)
        [Fact]
        public void TestLookup()
        {
            RequireServer.Check().Supports(Feature.Csfle2QEv2Lookup)
                .ClusterTypes(ClusterType.ReplicaSet, ClusterType.Sharded, ClusterType.LoadBalanced);

            TestLookupSetup();

            var keyVaultCollectionNamespace = new CollectionNamespace("db", "keyvault");
            var csfleNamespace = new CollectionNamespace("db", "csfle");
            var qeNamespace = new CollectionNamespace("db", "qe");
            var noSchemaNamespace = new CollectionNamespace("db", "no_schema");

            // Case 1: db.csfle joins db.no_schema
            var pipeline1 = """
                           [
                               { "$match": { "csfle": "csfle" } },
                               {
                                   "$lookup": {
                                       "from": "no_schema",
                                       "as": "matched",
                                       "pipeline": [
                                           { "$match": { "no_schema": "no_schema" } },
                                           { "$project": { "_id": 0 } }
                                       ]
                                   }
                               },
                               { "$project": { "_id": 0 } }
                           ]
                           """;
            var expectedResult1 ="""{"csfle" : "csfle", "matched" : [ {"no_schema" : "no_schema"} ]}""";
            RunTestCase(csfleNamespace, pipeline1, expectedResult1);

            // Case 2: db.qe joins db.no_schema
            var pipeline2 = """
                           [
                               {"$match" : {"qe" : "qe"}},
                               {
                                  "$lookup" : {
                                     "from" : "no_schema",
                                     "as" : "matched",
                                     "pipeline" :
                                        [ {"$match" : {"no_schema" : "no_schema"}}, {"$project" : {"_id" : 0, "__safeContent__" : 0}} ]
                                  }
                               },
                               {"$project" : {"_id" : 0, "__safeContent__" : 0}}
                           ]
                           """;
            var expectedResult2 ="""{"qe" : "qe", "matched" : [ {"no_schema" : "no_schema"} ]}""";
            RunTestCase(qeNamespace, pipeline2, expectedResult2);

            // Case 3: db.no_schema joins db.csfle
            var pipeline3 = """
                           [
                               {"$match" : {"no_schema" : "no_schema"}},
                               {
                                   "$lookup" : {
                                       "from" : "csfle",
                                       "as" : "matched",
                                       "pipeline" : [ {"$match" : {"csfle" : "csfle"}}, {"$project" : {"_id" : 0}} ]
                                   }
                               },
                               {"$project" : {"_id" : 0}}
                           ]
                           """;
            var expectedResult3 ="""{"no_schema" : "no_schema", "matched" : [ {"csfle" : "csfle"} ]}""";
            RunTestCase(noSchemaNamespace, pipeline3, expectedResult3);

            // Case 4: db.no_schema joins db.qe
            var pipeline4 = """
                           [
                              {"$match" : {"no_schema" : "no_schema"}},
                              {
                                 "$lookup" : {
                                    "from" : "qe",
                                    "as" : "matched",
                                    "pipeline" : [ {"$match" : {"qe" : "qe"}}, {"$project" : {"_id" : 0, "__safeContent__" : 0}} ]
                                 }
                              },
                              {"$project" : {"_id" : 0}}
                           ]
                           """;
            var expectedResult4 ="""{"no_schema" : "no_schema", "matched" : [ {"qe" : "qe"} ]}""";
            RunTestCase(noSchemaNamespace, pipeline4, expectedResult4);

            // Case 5: db.csfle joins db.csfle2
            var pipeline5 = """
                           [
                              {"$match" : {"csfle" : "csfle"}},
                              {
                                 "$lookup" : {
                                    "from" : "csfle2",
                                    "as" : "matched",
                                    "pipeline" : [ {"$match" : {"csfle2" : "csfle2"}}, {"$project" : {"_id" : 0}} ]
                                 }
                              },
                              {"$project" : {"_id" : 0}}
                           ]
                           """;
            var expectedResult5 ="""{"csfle" : "csfle", "matched" : [ {"csfle2" : "csfle2"} ]}""";
            RunTestCase(csfleNamespace, pipeline5, expectedResult5);

            // Case 6: db.qe joins db.qe2
            var pipeline6 = """
                           [
                              {"$match" : {"qe" : "qe"}},
                              {
                                 "$lookup" : {
                                    "from" : "qe2",
                                    "as" : "matched",
                                    "pipeline" : [ {"$match" : {"qe2" : "qe2"}}, {"$project" : {"_id" : 0, "__safeContent__" : 0}} ]
                                 }
                              },
                              {"$project" : {"_id" : 0, "__safeContent__" : 0}}
                           ]
                           """;
            var expectedResult6 ="""{"qe" : "qe", "matched" : [ {"qe2" : "qe2"} ]}""";
            RunTestCase(qeNamespace, pipeline6, expectedResult6);

            // Case 7: db.no_schema joins db.no_schema2
            var pipeline7 = """
                           [
                               {"$match" : {"no_schema" : "no_schema"}},
                               {
                                   "$lookup" : {
                                       "from" : "no_schema2",
                                       "as" : "matched",
                                       "pipeline" : [ {"$match" : {"no_schema2" : "no_schema2"}}, {"$project" : {"_id" : 0}} ]
                                   }
                               },
                               {"$project" : {"_id" : 0}}
                           ]
                           """;
            var expectedResult7 ="""{"no_schema" : "no_schema", "matched" : [ {"no_schema2" : "no_schema2"} ]}""";
            RunTestCase(noSchemaNamespace, pipeline7, expectedResult7);

            // Case 8: db.csfle joins db.qe
            var pipeline8 = """
                            [
                                {"$match" : {"csfle" : "qe"}},
                                {
                                    "$lookup" : {
                                        "from" : "qe",
                                        "as" : "matched",
                                        "pipeline" : [ {"$match" : {"qe" : "qe"}}, {"$project" : {"_id" : 0}} ]
                                    }
                                },
                                {"$project" : {"_id" : 0}}
                            ]
                            """;

            var exception = Record.Exception(() => RunTestCase(csfleNamespace, pipeline8, null));
            exception.Should().NotBeNull();
            exception.Message.Should().Contain("not supported");

            void RunTestCase(CollectionNamespace collectionNamespace, string pipeline, string expectedResult)
            {
                using var mongoClient = ConfigureClientEncrypted(kmsProviderFilter: "local",
                    keyVaultCollectionNamespace: keyVaultCollectionNamespace);
                var collection = GetCollection(mongoClient, collectionNamespace);
                var result = collection.Aggregate(CreatePipeline(pipeline)).Single();
                var expectedBsonResult = BsonDocument.Parse(expectedResult);
                result.Should().Be(expectedBsonResult);
            }

            PipelineDefinition<BsonDocument, BsonDocument> CreatePipeline(string pipelineJson)
            {
                return Bson.Serialization.BsonSerializer.Deserialize<List<BsonDocument>>(pipelineJson);
            }
        }

        // 25. Test $lookup (case 9)
        [Fact]
        public void TestLookupUnsupported()
        {
            RequireServer.Check().Supports(Feature.Csfle2QEv2).DoesNotSupport(Feature.Csfle2QEv2Lookup)
                .ClusterTypes(ClusterType.ReplicaSet, ClusterType.Sharded, ClusterType.LoadBalanced);

            TestLookupSetup();

            var keyVaultCollectionNamespace = new CollectionNamespace("db", "keyvault");
            var csfleNamespace = new CollectionNamespace("db", "csfle");

            // Case 9: test error with <8.1
            var pipeline9 = """
                            [
                                {"$match" : {"csfle" : "qe"}},
                                {
                                    "$lookup" : {
                                        "from" : "qe",
                                        "as" : "matched",
                                        "pipeline" : [ {"$match" : {"qe" : "qe"}}, {"$project" : {"_id" : 0}} ]
                                    }
                                },
                                {"$project" : {"_id" : 0}}
                            ]
                            """;

            var exception = Record.Exception(() => RunTestCase(csfleNamespace, pipeline9));
            exception.Should().NotBeNull();
            exception.Message.Should().Contain("Upgrade");

            void RunTestCase(CollectionNamespace collectionNamespace, string pipeline)
            {
                using var mongoClient = ConfigureClientEncrypted(kmsProviderFilter: "local",
                    keyVaultCollectionNamespace: keyVaultCollectionNamespace);
                var collection = GetCollection(mongoClient, collectionNamespace);
                collection.Aggregate(CreatePipeline(pipeline)).Single();
            }

            PipelineDefinition<BsonDocument, BsonDocument> CreatePipeline(string pipelineJson)
            {
                return Bson.Serialization.BsonSerializer.Deserialize<List<BsonDocument>>(pipelineJson);
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void ViewAreProhibitedTest([Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            var viewName = CollectionNamespace.FromFullName("db.view");
            using (var client = ConfigureClient(false))
            using (var clientEncrypted = ConfigureClientEncrypted(kmsProviderFilter: "local"))
            {
                DropCollection(viewName);
                client
                    .GetDatabase(viewName.DatabaseNamespace.DatabaseName)
                    .CreateView(
                        viewName.CollectionName,
                        __collCollectionNamespace.CollectionName,
                        new EmptyPipelineDefinition<BsonDocument>());

                var view = GetCollection(clientEncrypted, viewName);
                var exception = Record.Exception(
                    () => Insert(
                        view,
                        async,
                        documents: new BsonDocument("test", 1)));
                exception.Message.Contains("cannot auto encrypt a view");
            }
        }

        [Theory]
        [ParameterAttributeData]
        public void UniqueIndexOnKeyAltNames(
            [Range(1, 2)] int testCase,
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            using (var client = ConfigureClient(clearCollections: true, writeConcern: WriteConcern.WMajority))
            {
                var keyVaultCollection = GetCollection(client, __keyVaultCollectionNamespace);
                keyVaultCollection.Indexes.CreateOne(
                    new CreateIndexModel<BsonDocument>(
                        new BsonDocument("keyAltNames", 1),
                        new CreateIndexOptions<BsonDocument>()
                        {
                            Name = "keyAltNames_1",
                            Unique = true,
                            PartialFilterExpression = new BsonDocument("keyAltNames", new BsonDocument("$exists", true))
                        }));

                using (var clientEncryption = ConfigureClientEncryption(client, kmsProviderFilter: "local"))
                {
                    var dataKey = CreateDataKey(clientEncryption, "local", new DataKeyOptions(alternateKeyNames: new[] { "def" }), async);
                    RunTestCase(clientEncryption, dataKey, testCase);
                }
            }

            void RunTestCase(ClientEncryption clientEncryption, Guid existingKey, int testCase)
            {
                switch (testCase)
                {
                    case 1:
                        {
                            var newLocalDataKey = CreateDataKey(clientEncryption, "local", new DataKeyOptions(alternateKeyNames: new[] { "abc" }), async);

                            var exception = Record.Exception(() => CreateDataKey(clientEncryption, "local", new DataKeyOptions(alternateKeyNames: new[] { "abc" }), async));
                            AssertInnerEncryptionException<MongoWriteException>(
                                exception,
                                ex => ex.WriteError.Code.Should().Be((int)ServerErrorCode.DuplicateKey));

                            exception = Record.Exception(() => CreateDataKey(clientEncryption, "local", new DataKeyOptions(alternateKeyNames: new[] { "def" }), async));
                            AssertInnerEncryptionException<MongoWriteException>(
                                exception,
                                ex => ex.WriteError.Code.Should().Be((int)ServerErrorCode.DuplicateKey));
                        }
                        break;
                    case 2:
                        {
                            // 1 create a new local data key and assert the operation does not fail.
                            var newLocalDataKey = CreateDataKey(clientEncryption, "local", new DataKeyOptions(), async);
                            // 2 add a keyAltName "abc" to the key created in Step 1 and assert the operation does not fail.
                            AddAlternateKeyName(clientEncryption, newLocalDataKey, alternateKeyName: "abc", async);
                            // 3 Repeat Step 2 and assert the returned key document contains the keyAltName "abc" added in Step 2.
                            var result = AddAlternateKeyName(clientEncryption, newLocalDataKey, alternateKeyName: "abc", async);
                            result["keyAltNames"].AsBsonArray.Contains("abc");
                            // 4 Add a keyAltName "def" to the key created in Step 1 and assert the operation fails due to a duplicate key
                            var exception = Record.Exception(() => AddAlternateKeyName(clientEncryption, newLocalDataKey, alternateKeyName: "def", async));
                            AssertInnerEncryptionException<MongoCommandException>(
                                exception,
                                ex => ex.Code.Should().Be((int)ServerErrorCode.DuplicateKey));
                            // 5 add a keyAltName "def" to the existing key, assert the operation does not fail, and assert the returned key document contains the keyAltName "def"
                            result = AddAlternateKeyName(clientEncryption, existingKey, "def", async);
                            result["keyAltNames"].AsBsonArray.Contains("def");
                        }
                        break;
                    default: throw new Exception($"Unexpected test case {testCase}.");
                }
            }
        }

        // NOTE: this test is not presented in the prose tests
        [Theory]
        [ParameterAttributeData]
        public void UnsupportedPlatformsTests(
            [Values("gcp")] string kmsProvider, // the rest kms providers are supported on all supported TFs
            [Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.ClientSideEncryption);

            using (var clientEncrypted = ConfigureClientEncrypted(kmsProviderFilter: kmsProvider))
            using (var clientEncryption = ConfigureClientEncryption(clientEncrypted, kmsProviderFilter: kmsProvider))
            {
                var dataKeyOptions = CreateDataKeyOptions(kmsProvider);
                var exception = Record.Exception(() => _ = CreateDataKey(clientEncryption, kmsProvider, dataKeyOptions, async));

                exception.Should().BeNull();
            }
        }

        // private methods
        private BsonDocument AddAlternateKeyName(
            ClientEncryption clientEncryption,
            Guid id,
            string alternateKeyName,
            bool async)
        {
            if (async)
            {
                return clientEncryption
                    .AddAlternateKeyNameAsync(id, alternateKeyName, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                return clientEncryption.AddAlternateKeyName(id, alternateKeyName, CancellationToken.None);
            }
        }

        private void AddOrReplace<TValue>(IDictionary<string, TValue> dict, string key, TValue value)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = value;
            }
            else
            {
                dict.Add(key, value);
            }
        }

        private void AssertInnerEncryptionException(Exception ex, Type innerExceptionType, string exceptionMessageContains)
            => AssertInnerEncryptionException(ex, innerExceptionType, e => e.Message.Should().Contain(exceptionMessageContains));

        private void AssertInnerEncryptionException(Exception ex, Type innerExceptionType, Action<Exception> assert = null)
        {
            ex.Should().BeOfType<MongoEncryptionException>();
            Exception e = ex;
            while (e != null && !innerExceptionType.IsAssignableFrom(e.GetType()))
            {
                e = e.InnerException;
            }

            e.Should().NotBeNull($"Cannot find inner exception of expected type: {innerExceptionType}.");
            assert?.Invoke(e);
        }

        private void AssertInnerEncryptionException<TInnerException>(Exception ex, Action<TInnerException> assert = null)
            where TInnerException : Exception
            => AssertInnerEncryptionException(ex, typeof(TInnerException), ex => assert?.Invoke((TInnerException)ex));

        private void AssertInnerEncryptionException<TInnerException>(Exception ex, string exceptionMessageContains)
            where TInnerException : Exception
            => AssertInnerEncryptionException<TInnerException>(ex, e => e.Message.Should().Contain(exceptionMessageContains));

        private void AssertInnerEncryptionExceptionRegex<TInnerException>(Exception ex, string exceptionMessageRegex)
            where TInnerException : Exception
            => AssertInnerEncryptionException<TInnerException>(ex, e => e.Message.Should().MatchRegex(exceptionMessageRegex));

        private IMongoClient ConfigureClient(
            bool clearCollections = true,
            int? maxPoolSize = null,
            WriteConcern writeConcern = null,
            ReadConcern readConcern = null,
            CollectionNamespace mainCollectionNamespace = null,
            BsonDocument encryptedFields = null,
            CollectionNamespace keyVaultNamespace = null,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> kmsProviders = null)
        {
            var client = CreateMongoClient(maxPoolSize: maxPoolSize, writeConcern: writeConcern, readConcern: readConcern, keyVaultNamespace: keyVaultNamespace, kmsProviders: kmsProviders);
            if (clearCollections)
            {
                var clientKeyVaultDatabase = client.GetDatabase(__keyVaultCollectionNamespace.DatabaseNamespace.DatabaseName);
                clientKeyVaultDatabase.DropCollection(__keyVaultCollectionNamespace.CollectionName);
                mainCollectionNamespace = mainCollectionNamespace ?? __collCollectionNamespace;
                var clientDbDatabase = client.GetDatabase(mainCollectionNamespace.DatabaseNamespace.DatabaseName);
                clientDbDatabase.DropCollection(mainCollectionNamespace.CollectionName, new DropCollectionOptions { EncryptedFields = encryptedFields });
            }
            return client;
        }

        private IMongoClient ConfigureClientEncrypted(
            BsonDocument schemaMap = null,
            IMongoClient externalKeyVaultClient = null,
            string kmsProviderFilter = null,
            EventCapturer eventCapturer = null,
            Dictionary<string, object> extraOptions = null,
            bool bypassAutoEncryption = false,
            bool bypassQueryAnalysis = false,
            int? maxPoolSize = null,
            bool? retryReads = null,
            Func<AutoEncryptionOptions, AutoEncryptionOptions> autoEncryptionOptionsConfigurator = null,
            CollectionNamespace keyVaultCollectionNamespace = null)
        {
            var configuredSettings = ConfigureClientEncryptedSettings(
                schemaMap,
                externalKeyVaultClient,
                kmsProviderFilter,
                eventCapturer,
                extraOptions,
                bypassAutoEncryption,
                bypassQueryAnalysis,
                maxPoolSize,
                retryReads,
                keyVaultCollectionNamespace);

            if (autoEncryptionOptionsConfigurator != null)
            {
                configuredSettings.AutoEncryptionOptions = autoEncryptionOptionsConfigurator.Invoke(configuredSettings.AutoEncryptionOptions);
            }

            return DriverTestConfiguration.CreateMongoClient(configuredSettings);
        }

        private MongoClientSettings ConfigureClientEncryptedSettings(
            BsonDocument schemaMap = null,
            IMongoClient externalKeyVaultClient = null,
            string kmsProviderFilter = null,
            EventCapturer eventCapturer = null,
            Dictionary<string, object> extraOptions = null,
            bool bypassAutoEncryption = false,
            bool bypassQueryAnalysis = false,
            int? maxPoolSize = null,
            bool? retryReads = null,
            CollectionNamespace keyVaultCollectionNamespace = null)
        {
            var kmsProviders = EncryptionTestHelper.GetKmsProviders(filter: kmsProviderFilter);
            var tlsOptions = EncryptionTestHelper.CreateTlsOptionsIfAllowed(
                kmsProviders,
                // only kmip currently requires tls configuration for ClientEncrypted
                allowClientCertificateFunc: kmsProviderName => kmsProviderName.StartsWith("kmip"));

            var clientEncryptedSettings =
                CreateMongoClientSettings(
                    keyVaultNamespace: keyVaultCollectionNamespace ??__keyVaultCollectionNamespace,
                    schemaMapDocument: schemaMap,
                    kmsProviders: kmsProviders,
                    externalKeyVaultClient: externalKeyVaultClient,
                    eventCapturer: eventCapturer,
                    extraOptions: extraOptions,
                    bypassAutoEncryption: bypassAutoEncryption,
                    bypassQueryAnalysis: bypassQueryAnalysis,
                    maxPoolSize: maxPoolSize,
                    retryReads: retryReads);

            if (tlsOptions != null)
            {
                clientEncryptedSettings.AutoEncryptionOptions = clientEncryptedSettings.AutoEncryptionOptions.With(tlsOptions: tlsOptions);
            }

            return clientEncryptedSettings;
        }

        private ClientEncryption ConfigureClientEncryption(
            IMongoClient client,
            Action<string, Dictionary<string, object>> kmsProviderConfigurator = null,
            Func<string, bool> allowClientCertificateFunc = null,
            Action<ClientEncryptionOptions> clientEncryptionOptionsConfigurator = null,
            string kmsProviderFilter = null,
            BsonDocument kmsDocument = null)
        {
            Dictionary<string, IReadOnlyDictionary<string, object>> kmsProviders;
            if (kmsDocument == null)
            {
                kmsProviders = EncryptionTestHelper
                    .GetKmsProviders(filter: kmsProviderFilter)
                    .Select(k =>
                    {
                        if (kmsProviderConfigurator != null)
                        {
                            kmsProviderConfigurator(k.Key, (Dictionary<string, object>)k.Value);
                        }
                        return k;
                    })
                    .ToDictionary(k => k.Key, k => k.Value);
            }
            else
            {
                Ensure.IsNull(kmsProviderFilter, nameof(kmsProviderFilter));

                kmsProviders = kmsDocument
                    .Elements
                    .ToDictionary(
                        k => k.Name,
                        k => (IReadOnlyDictionary<string, object>)k.Value.AsBsonDocument.ToDictionary(ki => ki.Name, ki => (object)ki.Value));
            }

            allowClientCertificateFunc = allowClientCertificateFunc ?? ((kmsProviderName) => kmsProviderName.StartsWith("kmip")); // configure Tls for kmip by default
            var tlsOptions = EncryptionTestHelper.CreateTlsOptionsIfAllowed(kmsProviders, allowClientCertificateFunc);

            var clientEncryptionOptions = new ClientEncryptionOptions(
                keyVaultClient: client.Settings.AutoEncryptionOptions?.KeyVaultClient ?? client,
                keyVaultNamespace: __keyVaultCollectionNamespace,
                kmsProviders: kmsProviders);

            if (tlsOptions != null)
            {
                clientEncryptionOptions = clientEncryptionOptions.With(tlsOptions: tlsOptions);
            }

            clientEncryptionOptionsConfigurator?.Invoke(clientEncryptionOptions);

            return new ClientEncryption(clientEncryptionOptions);
        }

        private void CreateCollection(IMongoClient client, CollectionNamespace collectionNamespace, BsonDocument validatorSchema = null, BsonDocument encryptedFields = null)
        {
            client
                .GetDatabase(collectionNamespace.DatabaseNamespace.DatabaseName)
                .CreateCollection(
                    collectionNamespace.CollectionName,
                    new CreateCollectionOptions<BsonDocument>()
                    {
                        EncryptedFields = encryptedFields,
                        Validator = validatorSchema != null ? new BsonDocumentFilterDefinition<BsonDocument>(validatorSchema) : null
                    });
        }

        private IMongoCollection<BsonDocument> CreateEncryptedCollection(IMongoClient client, ClientEncryption clientEncryption, CollectionNamespace collectionNamespace, BsonDocument encryptedFields, string kmsProvider, bool async, out BsonDocument effectiveEncryptedFields)
        {
            var createCollectionOptions = new CreateCollectionOptions { EncryptedFields = encryptedFields };
            return CreateEncryptedCollection<BsonDocument>(client, clientEncryption, collectionNamespace, createCollectionOptions, kmsProvider, async, out effectiveEncryptedFields);
        }

        private IMongoCollection<T> CreateEncryptedCollection<T>(IMongoClient client, ClientEncryption clientEncryption, CollectionNamespace collectionNamespace, BsonDocument encryptedFields, string kmsProvider, bool async, out BsonDocument effectiveEncryptedFields)
        {
            var createCollectionOptions = new CreateCollectionOptions { EncryptedFields = encryptedFields };
            return CreateEncryptedCollection<T>(client, clientEncryption, collectionNamespace, createCollectionOptions, kmsProvider, async, out effectiveEncryptedFields);
        }

        private IMongoCollection<T> CreateEncryptedCollection<T>(IMongoClient client, ClientEncryption clientEncryption, CollectionNamespace collectionNamespace, CreateCollectionOptions createCollectionOptions, string kmsProvider, bool async, out BsonDocument effectiveEncryptedFields)
        {
            var datakeyOptions = CreateDataKeyOptions(kmsProvider, alternateKeyNames: null);
            var database = client.GetDatabase(collectionNamespace.DatabaseNamespace.DatabaseName);


            var result = async
                ? clientEncryption.CreateEncryptedCollectionAsync(database, collectionNamespace.CollectionName, createCollectionOptions, kmsProvider, datakeyOptions.MasterKey, cancellationToken: default).GetAwaiter().GetResult()
                : clientEncryption.CreateEncryptedCollection(database, collectionNamespace.CollectionName, createCollectionOptions, kmsProvider, datakeyOptions.MasterKey, cancellationToken: default);

            effectiveEncryptedFields = result.EncryptedFields;

            return client.GetDatabase(collectionNamespace.DatabaseNamespace.DatabaseName).GetCollection<T>(collectionNamespace.CollectionName);
        }

        private Guid CreateDataKey(
            ClientEncryption clientEncryption,
            string kmsProvider,
            DataKeyOptions dataKeyOptions,
            bool async)
        {
            if (async)
            {
                return clientEncryption
                    .CreateDataKeyAsync(kmsProvider, dataKeyOptions, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                return clientEncryption.CreateDataKey(kmsProvider, dataKeyOptions, CancellationToken.None);
            }
        }

        private DataKeyOptions CreateDataKeyOptions(string kmsProvider, BsonDocument customMasterKey = null)
        {
            var alternateKeyNames = new[] { $"{kmsProvider}_altname" };
            return CreateDataKeyOptions(kmsProvider, alternateKeyNames, customMasterKey);
        }

        private DataKeyOptions CreateDataKeyOptions(string kmsProvider, string[] alternateKeyNames, BsonDocument customMasterKey = null)
        {
            var masterKey = customMasterKey ?? EncryptionTestHelper.CreateMasterKey(kmsProvider);
            return new DataKeyOptions(
                alternateKeyNames: alternateKeyNames,
                masterKey: masterKey);
        }

        private RewrapManyDataKeyOptions CreateRewrapManyDataKeyOptions(string kmsProvider, BsonDocument customMasterKey = null)
        {
            var masterKey = customMasterKey ?? EncryptionTestHelper.CreateMasterKey(kmsProvider);
            return new RewrapManyDataKeyOptions(kmsProvider, masterKey: masterKey);
        }

        private IMongoClient CreateMongoClient(
            CollectionNamespace keyVaultNamespace = null,
            BsonDocument schemaMapDocument = null,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> kmsProviders = null,
            IMongoClient externalKeyVaultClient = null,
            EventCapturer eventCapturer = null,
            Dictionary<string, object> extraOptions = null,
            bool bypassAutoEncryption = false,
            bool bypassQueryAnalysis = false,
            int? maxPoolSize = null,
            WriteConcern writeConcern = null,
            ReadConcern readConcern = null)
        {
            var mongoClientSettings = CreateMongoClientSettings(
                keyVaultNamespace,
                schemaMapDocument,
                kmsProviders,
                externalKeyVaultClient,
                eventCapturer,
                extraOptions,
                bypassAutoEncryption,
                bypassQueryAnalysis,
                maxPoolSize,
                writeConcern,
                readConcern);

            return DriverTestConfiguration.CreateMongoClient(mongoClientSettings);
        }

        private MongoClientSettings CreateMongoClientSettings(
            CollectionNamespace keyVaultNamespace = null,
            BsonDocument schemaMapDocument = null,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> kmsProviders = null,
            IMongoClient externalKeyVaultClient = null,
            EventCapturer eventCapturer = null,
            Dictionary<string, object> extraOptions = null,
            bool bypassAutoEncryption = false,
            bool bypassQueryAnalysis = false,
            int? maxPoolSize = null,
            WriteConcern writeConcern = null,
            ReadConcern readConcern = null,
            bool? retryReads = null)
        {
            var mongoClientSettings = DriverTestConfiguration.GetClientSettings().Clone();
            if (eventCapturer != null)
            {
                mongoClientSettings.ClusterConfigurator = builder => builder.Subscribe(eventCapturer);
            }
            else
            {
                var globalClusterConfiguratorAction = mongoClientSettings.ClusterConfigurator;
                mongoClientSettings.ClusterConfigurator = (b) => globalClusterConfiguratorAction(b); // we need to have a new instance of ClusterConfigurator
            }

            if (maxPoolSize.HasValue)
            {
                mongoClientSettings.MaxConnectionPoolSize = maxPoolSize.Value;
            }

            if (writeConcern != null)
            {
                mongoClientSettings.WriteConcern = writeConcern;
            }

            if (readConcern != null)
            {
                mongoClientSettings.ReadConcern = readConcern;
            }

            if (retryReads.HasValue)
            {
                mongoClientSettings.RetryReads = retryReads.Value;
            }

            if (keyVaultNamespace != null || schemaMapDocument != null || kmsProviders != null || externalKeyVaultClient != null)
            {
                if (extraOptions == null)
                {
                    extraOptions = new Dictionary<string, object>();
                }

                EncryptionTestHelper.ConfigureDefaultExtraOptions(extraOptions);

                var schemaMap = GetSchemaMapIfNotNull(schemaMapDocument);

                if (kmsProviders == null)
                {
                    kmsProviders = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>(new Dictionary<string, IReadOnlyDictionary<string, object>>());
                }

                var autoEncryptionOptions = new AutoEncryptionOptions(
                    keyVaultNamespace: keyVaultNamespace,
                    kmsProviders: kmsProviders,
                    schemaMap: schemaMap,
                    extraOptions: extraOptions,
                    bypassAutoEncryption: bypassAutoEncryption,
                    bypassQueryAnalysis: bypassQueryAnalysis);

                if (externalKeyVaultClient != null)
                {
                    autoEncryptionOptions = autoEncryptionOptions.With(keyVaultClient: Optional.Create(externalKeyVaultClient));
                }
                mongoClientSettings.AutoEncryptionOptions = autoEncryptionOptions;
            }

            mongoClientSettings.LoggingSettings = LoggingSettings;
            mongoClientSettings.ClusterSource = DisposingClusterSource.Instance;

            return mongoClientSettings;
        }

        private void DropCollection(CollectionNamespace collectionNamespace, BsonDocument encryptedFields = null)
        {
            var operation = DropCollectionOperation.CreateEncryptedDropCollectionOperationIfConfigured(collectionNamespace, encryptedFields, CoreTestConfiguration.MessageEncoderSettings, configureDropCollectionConfigurator: null);
            using (var session = CoreTestConfiguration.StartSession(_cluster))
            using (var binding = new WritableServerBinding(_cluster, session.Fork()))
            using (var bindingHandle = new ReadWriteBindingHandle(binding))
            {
                operation.Execute(OperationContext.NoTimeout, bindingHandle);
            }
        }

        private BsonValue ExplicitDecrypt(
            ClientEncryption clientEncryption,
            BsonBinaryData value,
            bool async)
        {
            BsonValue decryptedValue;
            if (async)
            {
                decryptedValue = clientEncryption
                    .DecryptAsync(
                        value,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                decryptedValue = clientEncryption.Decrypt(
                    value,
                    CancellationToken.None);
            }

            return decryptedValue;
        }

        private BsonBinaryData ExplicitEncrypt(
            ClientEncryption clientEncryption,
            EncryptOptions encryptOptions,
            BsonValue value,
            bool async)
        {
            BsonBinaryData encryptedValue;
            if (async)
            {
                encryptedValue = clientEncryption
                    .EncryptAsync(
                        value,
                        encryptOptions,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                encryptedValue = clientEncryption.Encrypt(
                    value,
                    encryptOptions,
                    CancellationToken.None);
            }

            return encryptedValue;
        }

        private async Task<BsonDocument> ExplicitEncryptExpression(
            ClientEncryption clientEncryption,
            EncryptOptions encryptOptions,
            BsonDocument expression,
            bool async) =>
            async
                ? await clientEncryption.EncryptExpressionAsync(expression, encryptOptions)
                : clientEncryption.EncryptExpression(expression, encryptOptions);

        private IAsyncCursor<BsonDocument> Find(
            IMongoCollection<BsonDocument> collection,
            BsonDocument filter,
            bool async)
        {
            if (async)
            {
                return collection
                    .FindAsync(new BsonDocumentFilterDefinition<BsonDocument>(filter))
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                return collection
                    .FindSync(new BsonDocumentFilterDefinition<BsonDocument>(filter));
            }
        }

        private IMongoCollection<BsonDocument> GetCollection(IMongoClient client, CollectionNamespace collectionNamespace)
        {
            var collectionSettings = new MongoCollectionSettings
            {
                ReadConcern = ReadConcern.Majority,
                WriteConcern = WriteConcern.WMajority
            };
            return client
                .GetDatabase(collectionNamespace.DatabaseNamespace.DatabaseName)
                .GetCollection<BsonDocument>(collectionNamespace.CollectionName, collectionSettings);
        }

        private Dictionary<string, BsonDocument> GetSchemaMapIfNotNull(BsonDocument schemaMapDocument)
        {
            Dictionary<string, BsonDocument> schemaMap = null;
            if (schemaMapDocument != null)
            {
                var element = schemaMapDocument.Single();
                schemaMap = new Dictionary<string, BsonDocument>
                    {
                        { element.Name, element.Value.AsBsonDocument }
                    };
            }
            return schemaMap;
        }

        private EventCapturer CreateEventCapturer(string commandNameFilter = null)
        {
            var defaultCommandsToNotCapture = new HashSet<string>
            {
                "hello",
                OppressiveLanguageConstants.LegacyHelloCommandName,
                "getLastError",
                "authenticate",
                "saslStart",
                "saslContinue",
                "getnonce"
            };

            return
                new EventCapturer()
                .Capture<CommandStartedEvent>(
                    e =>
                        !defaultCommandsToNotCapture.Contains(e.CommandName) &&
                        (commandNameFilter == null || e.CommandName == commandNameFilter));
        }

        private void Insert(
            IMongoCollection<BsonDocument> collection,
            bool async,
            params BsonDocument[] documents)
        {
            if (async)
            {
                collection
                    .InsertManyAsync(documents)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                collection.InsertMany(documents);
            }
        }

        private RewrapManyDataKeyResult RewrapManyDataKey(
            ClientEncryption clientEncryption,
            RewrapManyDataKeyOptions rewrapManyDataKeyOptions,
            bool async,
            string filter = "{}") =>
            async
                ? clientEncryption
                    .RewrapManyDataKeyAsync(
                        filter,
                        rewrapManyDataKeyOptions,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                : clientEncryption.RewrapManyDataKey(
                    filter,
                    rewrapManyDataKeyOptions,
                    CancellationToken.None);

        private void TestLookupSetup()
        {
            var keyVaultCollectionNamespace = new CollectionNamespace("db", "keyvault");
            var csfleNamespace = new CollectionNamespace("db", "csfle");
            var csfle2Namespace = new CollectionNamespace("db", "csfle2");
            var qeNamespace = new CollectionNamespace("db", "qe");
            var qe2Namespace = new CollectionNamespace("db", "qe2");
            var noSchemaNamespace = new CollectionNamespace("db", "no_schema");
            var noSchema2Namespace = new CollectionNamespace("db", "no_schema2");

            var keyDoc = JsonFileReader.Instance.Documents["etc.data.lookup.key-doc.json"];
            var schemaCsfle = JsonFileReader.Instance.Documents["etc.data.lookup.schema-csfle.json"];
            var schemaCsfle2 = JsonFileReader.Instance.Documents["etc.data.lookup.schema-csfle2.json"];
            var schemaQe = JsonFileReader.Instance.Documents["etc.data.lookup.schema-qe.json"];
            var schemaQe2 = JsonFileReader.Instance.Documents["etc.data.lookup.schema-qe2.json"];

            // Setup
            using var clientEncrypted = ConfigureClientEncrypted(kmsProviderFilter: "local",
                keyVaultCollectionNamespace: keyVaultCollectionNamespace);
            using var client = ConfigureClient();

            DropCollection(keyVaultCollectionNamespace);
            DropCollection(csfleNamespace);
            DropCollection(csfle2Namespace);
            DropCollection(qeNamespace);
            DropCollection(qe2Namespace);
            DropCollection(noSchemaNamespace);
            DropCollection(noSchema2Namespace);

            CreateCollection(clientEncrypted, csfleNamespace,
                validatorSchema: new BsonDocument("$jsonSchema", schemaCsfle));
            CreateCollection(clientEncrypted, csfle2Namespace,
                validatorSchema: new BsonDocument("$jsonSchema", schemaCsfle2));
            CreateCollection(clientEncrypted, qeNamespace, encryptedFields: schemaQe);
            CreateCollection(clientEncrypted, qe2Namespace, encryptedFields: schemaQe2);
            CreateCollection(clientEncrypted, noSchemaNamespace);
            CreateCollection(clientEncrypted, noSchema2Namespace);

            // Collections from encrypted client
            var keyVaultCollectionEncrypted = GetCollection(clientEncrypted, keyVaultCollectionNamespace);
            var csfleCollectionEncrypted = GetCollection(clientEncrypted, csfleNamespace);
            var csfle2CollectionEncrypted = GetCollection(clientEncrypted, csfle2Namespace);
            var qeCollectionEncrypted = GetCollection(clientEncrypted, qeNamespace);
            var qe2CollectionEncrypted = GetCollection(clientEncrypted, qe2Namespace);
            var noSchemaCollectionEncrypted = GetCollection(clientEncrypted, noSchemaNamespace);
            var noSchema2CollectionEncrypted = GetCollection(clientEncrypted, noSchema2Namespace);

            // Collections from plain (unencrypted) client
            var csfleCollection = GetCollection(client, csfleNamespace);
            var csfle2Collection = GetCollection(client, csfle2Namespace);
            var qeCollection = GetCollection(client, qeNamespace);
            var qe2Collection = GetCollection(client, qe2Namespace);

            keyVaultCollectionEncrypted.InsertOne(keyDoc);

            // Insert with encrypted and retrieve with plain client
            var emptyFilter = new BsonDocument();

            csfleCollectionEncrypted.InsertOne(BsonDocument.Parse("""{"csfle": "csfle"}"""));
            var c1 = Find(csfleCollection, emptyFilter, false).Single();
            c1["csfle"].BsonType.Should().Be(BsonType.Binary);

            csfle2CollectionEncrypted.InsertOne(BsonDocument.Parse("""{"csfle2": "csfle2"}"""));
            var c2 = Find(csfle2Collection, emptyFilter, false).Single();
            c2["csfle2"].BsonType.Should().Be(BsonType.Binary);

            qeCollectionEncrypted.InsertOne(BsonDocument.Parse("""{"qe": "qe"}"""));
            var q1 = Find(qeCollection, emptyFilter, false).Single();
            q1["qe"].BsonType.Should().Be(BsonType.Binary);

            qe2CollectionEncrypted.InsertOne(BsonDocument.Parse("""{"qe2": "qe2"}"""));
            var q2 = Find(qe2Collection, emptyFilter, false).Single();
            q2["qe2"].BsonType.Should().Be(BsonType.Binary);

            noSchemaCollectionEncrypted.InsertOne(BsonDocument.Parse("""{"no_schema": "no_schema"}"""));
            noSchema2CollectionEncrypted.InsertOne(BsonDocument.Parse("""{"no_schema2": "no_schema2"}"""));
        }

        // nested types
        public enum CertificateType
        {
            TlsWithClientCert,
            TlsWithoutClientCert,
            Expired,
            InvalidHostName
        }

        public class JsonFileReader : EmbeddedResourceJsonFileReader
        {
            #region static
            // private static fields
            private static readonly string[] __ignoreKeyNames =
            {
                "dbPointer" // not supported
            };
            private static readonly Lazy<JsonFileReader> __instance = new Lazy<JsonFileReader>(() => new JsonFileReader(), isThreadSafe: true);

            // public static properties
            public static JsonFileReader Instance => __instance.Value;
            #endregion

            private readonly IReadOnlyDictionary<string, BsonDocument> _documents;

            public JsonFileReader()
            {
                _documents = new ReadOnlyDictionary<string, BsonDocument>(ReadDocuments());
            }

            protected override string[] PathPrefixes => new[]
            {
                "MongoDB.Driver.Tests.Specifications.client_side_encryption.prose_tests.corpus.",
                "MongoDB.Driver.Tests.Specifications.client_side_encryption.prose_tests.external.",
                "MongoDB.Driver.Tests.Specifications.client_side_encryption.prose_tests.limits.",
                "MongoDB.Driver.Tests.Specifications.client_side_encryption.prose_tests.etc.data.",
                "MongoDB.Driver.Tests.Specifications.client_side_encryption.prose_tests.etc.data.keys"
            };

            public IReadOnlyDictionary<string, BsonDocument> Documents
            {
                get
                {
                    return _documents.ToDictionary(k => k.Key, v => v.Value.DeepClone().AsBsonDocument);
                }
            }

            // private methods
            private IDictionary<string, BsonDocument> ReadDocuments()
            {
                var documents = ReadJsonDocuments();
                return new Dictionary<string, BsonDocument>(
                    documents.ToDictionary(
                        key =>
                        {
                            var path = key["_path"].ToString();
                            var testTitle = "MongoDB.Driver.Tests.Specifications.client_side_encryption.prose_tests";
                            var startIndex = path.IndexOf(testTitle, StringComparison.Ordinal);
                            if (startIndex != -1)
                            {
                                return path.Substring(startIndex + testTitle.Length + 1);
                            }
                            else
                            {
                                throw new ArgumentException($"Unexpected test file: {path}.");
                            }
                        },
                        value =>
                        {
                            RemoveIgnoredElements(value);
                            return value;
                        }));
            }

            private void RemoveIgnoredElements(BsonDocument document)
            {
                document.Remove("_path");
                var ignoredElements = document
                    .Where(c => __ignoreKeyNames.Any(i => c.Name.Contains(i)))
                    .ToList();
                foreach (var ignored in ignoredElements.Where(c => c.Value.IsBsonDocument))
                {
                    document.RemoveElement(ignored);
                }
            }
        }

        private class HttpClientWrapperWithModifiedRequest : IHttpClientWrapper
        {
            private readonly IHttpClientWrapper _httpClientWrapper;
            private readonly Action<HttpRequestMessage> _modifyAction;

            public HttpClientWrapperWithModifiedRequest(
                IHttpClientWrapper httpClientWrapper,
                Action<HttpRequestMessage> modifyAction)
            {
                _httpClientWrapper = Ensure.IsNotNull(httpClientWrapper, nameof(httpClientWrapper));
                _modifyAction = Ensure.IsNotNull(modifyAction, nameof(modifyAction));
            }

            public Task<string> GetHttpContentAsync(HttpRequestMessage request, string exceptionMessage, CancellationToken cancellationToken)
            {
                _modifyAction(request);
                return _httpClientWrapper.GetHttpContentAsync(request, exceptionMessage, cancellationToken);
            }
        }

        private class Patient
        {
            public ObjectId Id { get; set; }
            public string Name { get; set; }
            public string Ssn { get; set; }
        }
    }

    public static class ClientEncryptionOptionsReflector
    {
        public static void _tlsOptions(this ClientEncryptionOptions obj, IReadOnlyDictionary<string, SslSettings> tlsOptions) => Reflector.SetFieldValue(obj, nameof(_tlsOptions), tlsOptions);
    }
}
