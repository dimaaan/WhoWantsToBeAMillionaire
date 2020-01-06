using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;

class EventLogger
{
    readonly IMongoCollection<BsonDocument> Events;
    readonly ILogger<EventLogger> Logger;

    public EventLogger(string connectionString, string databaseName, string collectionName, ILogger<EventLogger> logger)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);

        settings.RetryWrites = false;

        var client = new MongoClient(settings);
        var database = client.GetDatabase(databaseName);

        Events = database.GetCollection<BsonDocument>(collectionName);
        Logger = logger;
    }

    public async Task LogStartGame(Message msg, CancellationToken cancellationToken)
    {
        await Log(msg, null, null, cancellationToken, new BsonElement("started", true));
    }

    public async Task LogAnswer(Message msg, byte level, short question, char answer1, char answer2, bool right, CancellationToken cancellationToken)
    {
        await Log(msg, level, question, cancellationToken,
            new BsonElement("answer", answer2 == default ? answer1.ToString() : $"{answer1}{answer2}"),
            new BsonElement("right", right));
    }

    public async Task LogHint(Message msg, byte level, short question, string hint, CancellationToken cancellationToken)
    {
        await Log(msg, level, question, cancellationToken, new BsonElement("hint", hint));
    }

    async Task Log(Message msg, byte? level, short? question, CancellationToken cancellationToken, params BsonElement[] values)
    {
        try
        {
            var doc = new BsonDocument
            {
                ["chat"] = msg.chat.id,
                ["date"] = new BsonDateTime(((long)msg.date) * 1000),
            };

            if (level != null)
                doc.Add("level", level);

            if (question != null)
                doc.Add("question", question);

            doc.AddRange(values);

            await Events.InsertOneAsync(doc, null, cancellationToken);
        }
        catch(Exception e)
        {
            Logger.LogWarning(e, "Failed to save event to MongoDB");
        }
    }
}