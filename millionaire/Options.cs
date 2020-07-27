/// <summary>
/// Telegram Bot API related options
/// </summary>
public class TelegramOptions
{
    public string ApiKey { get; set; } = default!;
    public string WebhookAddress { get; set; } = default!;
    public string? Certificate { get; set; }
}

/// <summary>
/// MongoDB related options
/// </summary>
public class MongoOptions
{
    public string ConnectionString { get; set; } = default!;
    public string Database { get; set; } = default!;
    public string EventCollection { get; set; } = default!;
    public string UserInfoCollection { get; set; } = default!;
}