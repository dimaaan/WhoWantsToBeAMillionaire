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
/// Sqlite related options
/// </summary>
public class SqliteOptions
{
    public string ConnectionString { get; set; } = default!;
}