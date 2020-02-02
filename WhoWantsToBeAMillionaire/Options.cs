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