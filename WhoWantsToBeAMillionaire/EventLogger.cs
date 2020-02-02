﻿using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Log game events for analytic purposes
/// </summary>
class EventLogger
{
    readonly IMongoCollection<BsonDocument> Events;
    readonly IMongoCollection<User> UserInfo;
    readonly ILogger<EventLogger> Logger;

    public EventLogger(MongoOptions options, ILogger<EventLogger> logger)
    {
        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);

        settings.RetryWrites = false;

        var client = new MongoClient(settings);
        var database = client.GetDatabase(options.Database);

        Events = database.GetCollection<BsonDocument>(options.EventCollection);
        UserInfo = database.GetCollection<User>(options.UserInfoCollection);
        Logger = logger;
    }

    public void StartGame(Message msg, CancellationToken cancellationToken)
    {
        LogEvent(msg, null, null, cancellationToken, new BsonElement("started", true));
        LogUserInfo(msg.from, cancellationToken);
    }

    public void Answer(Message msg, byte level, short question, char answer1, char answer2, bool right, CancellationToken cancellationToken)
    {
        LogEvent(msg, level, question, cancellationToken,
            new BsonElement("answer", answer2 == default ? answer1.ToString() : $"{answer1}{answer2}"),
            new BsonElement("right", right));
    }

    public void Hint(Message msg, byte level, short question, string hint, CancellationToken cancellationToken)
    {
        LogEvent(msg, level, question, cancellationToken, new BsonElement("hint", hint));
    }

    /// <summary>
    /// Logs event in fire-and-forget style
    /// </summary>
    void LogEvent(Message msg, byte? level, short? question, CancellationToken cancellationToken, params BsonElement[] values)
    {
        Task.Run(async () =>
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
            catch (Exception e)
            {
                Logger.LogWarning(e, "Failed to save event to MongoDB");
            }
        });
    }

    static readonly ReplaceOptions UserReplaceOptions = new ReplaceOptions {
        IsUpsert = true
    };

    void LogUserInfo(User user, CancellationToken cancellationToken)
    {
        Task.Run(async () => { 
            try
            {
                await UserInfo.ReplaceOneAsync(u => u.id == user.id, user, UserReplaceOptions, cancellationToken);
            }
            catch(Exception e)
            {
                Logger.LogWarning(e, "Failed to save user info to MongoDB");
            }
        });
    }
}