using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Events;
using Xunit;
using Xunit.Abstractions;

namespace MongoDB.Driver.Tests
{
    [Trait("Category", "Integration")]
    public class TempCursorLeakRepro
    {
        private readonly ITestOutputHelper _output;

        public TempCursorLeakRepro(ITestOutputHelper output)
        {
            _output = output;
        }

        public class Strict
        {
            public ObjectId Id { get; set; }
            public int X { get; set; }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Find_first_batch_deserialization_failure(bool async)
        {
            var eventCapturer = new EventCapturer().Capture<CommandStartedEvent>();
            using var client = DriverTestConfiguration.CreateMongoClient(eventCapturer);
            var db = client.GetDatabase("cursorleak");
            db.DropCollection("find");
            var raw = db.GetCollection<BsonDocument>("find");
            raw.InsertMany(Enumerable.Range(0, 20).Select(i => new BsonDocument { { "x", i }, { "unexpected", i } }).ToList());

            var openBefore = OpenCursors(client);
            var typed = db.GetCollection<Strict>("find");
            System.Exception caught = null;
            try
            {
                if (async)
                {
                    await typed.FindAsync(FilterDefinition<Strict>.Empty, new FindOptions<Strict> { BatchSize = 2 });
                }
                else
                {
                    typed.FindSync(FilterDefinition<Strict>.Empty, new FindOptions<Strict> { BatchSize = 2 });
                }
            }
            catch (System.Exception ex)
            {
                caught = ex;
            }

            var openAfter = OpenCursors(client);
            var commands = eventCapturer.Events.OfType<CommandStartedEvent>().Select(e => e.CommandName).ToList();
            _output.WriteLine($"async={async} exception={caught?.GetType().Name}: {caught?.Message}");
            _output.WriteLine($"commands=[{string.Join(", ", commands)}]");
            _output.WriteLine($"openCursors before={openBefore} after={openAfter}");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Aggregate_first_batch_deserialization_failure(bool async)
        {
            var eventCapturer = new EventCapturer().Capture<CommandStartedEvent>();
            using var client = DriverTestConfiguration.CreateMongoClient(eventCapturer);
            var db = client.GetDatabase("cursorleak");
            db.DropCollection("agg");
            var raw = db.GetCollection<BsonDocument>("agg");
            raw.InsertMany(Enumerable.Range(0, 20).Select(i => new BsonDocument { { "x", i }, { "unexpected", i } }).ToList());

            var openBefore = OpenCursors(client);
            var typed = db.GetCollection<Strict>("agg");
            PipelineDefinition<Strict, Strict> pipeline = new EmptyPipelineDefinition<Strict>();
            System.Exception caught = null;
            try
            {
                if (async)
                {
                    await typed.AggregateAsync(pipeline, new AggregateOptions { BatchSize = 2 });
                }
                else
                {
                    typed.Aggregate(pipeline, new AggregateOptions { BatchSize = 2 });
                }
            }
            catch (System.Exception ex)
            {
                caught = ex;
            }

            var openAfter = OpenCursors(client);
            var commands = eventCapturer.Events.OfType<CommandStartedEvent>().Select(e => e.CommandName).ToList();
            _output.WriteLine($"async={async} exception={caught?.GetType().Name}: {caught?.Message}");
            _output.WriteLine($"commands=[{string.Join(", ", commands)}]");
            _output.WriteLine($"openCursors before={openBefore} after={openAfter}");
        }

        [Fact]
        public void Baseline_getMore_deserialization_failure_kills_cursor()
        {
            var eventCapturer = new EventCapturer().Capture<CommandStartedEvent>();
            using var client = DriverTestConfiguration.CreateMongoClient(eventCapturer);
            var db = client.GetDatabase("cursorleak");
            db.DropCollection("getmore");
            var raw = db.GetCollection<BsonDocument>("getmore");
            var docs = new List<BsonDocument>();
            for (var i = 0; i < 20; i++)
            {
                var d = new BsonDocument("X", i);
                if (i >= 5)
                {
                    d.Add("unexpected", i);
                }
                docs.Add(d);
            }
            raw.InsertMany(docs);

            var openBefore = OpenCursors(client);
            var typed = db.GetCollection<Strict>("getmore");
            System.Exception caught = null;
            try
            {
                _ = typed.Find(FilterDefinition<Strict>.Empty, new FindOptions { BatchSize = 2 }).ToList();
            }
            catch (System.Exception ex)
            {
                caught = ex;
            }

            var openAfter = OpenCursors(client);
            var commands = eventCapturer.Events.OfType<CommandStartedEvent>().Select(e => e.CommandName).ToList();
            _output.WriteLine($"exception={caught?.GetType().Name}: {caught?.Message}");
            _output.WriteLine($"commands=[{string.Join(", ", commands)}]");
            _output.WriteLine($"openCursors before={openBefore} after={openAfter}");
            caught.Should().NotBeNull();
        }

        private static int OpenCursors(IMongoClient client)
        {
            var result = client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument { { "serverStatus", 1 } });
            return result["metrics"]["cursor"]["open"]["total"].ToInt32();
        }
    }
}
