using BotApi;
using MongoDB.Bson;
using MongoDB.Driver;

public class DbContext
{
    public DbContext(MongoOptions options)
    {
        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);

        settings.RetryWrites = false;

        var client = new MongoClient(settings);
        var database = client.GetDatabase(options.Database);

        Events = database.GetCollection<BsonDocument>(options.EventCollection);
        UserInfo = database.GetCollection<User>(options.UserInfoCollection);
    }

    public readonly IMongoCollection<BsonDocument> Events;

    public readonly IMongoCollection<User> UserInfo;
}
