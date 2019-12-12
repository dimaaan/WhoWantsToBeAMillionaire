using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class BotApiClient
{
    readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        IgnoreNullValues = true
    };
    readonly HttpClient Client;

    public BotApiClient(HttpClient client)
    {
        Client = client;
    }

    public async Task<User> GetMe(CancellationToken cancellationToken)
    {
        return await Get<User>("getMe", cancellationToken);
    }

    public async Task<WebhookInfo> GetWebhookInfo(CancellationToken cancellationToken)
    {
        return await Get<WebhookInfo>("getWebhookInfo", cancellationToken);
    }

    public async Task SetWebHook(string uri, string certificatePath, CancellationToken cancellationToken)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(uri), "url" },
            { new ByteArrayContent(File.ReadAllBytes(certificatePath)), "certificate", certificatePath }
        };

        var result = await Post<bool>("setWebhook", content, cancellationToken);

        if (!result)
            throw new TelegramException("Setting webhook failed");
    }

    public async Task DeleteWebhook(CancellationToken cancellationToken)
    {
        await Post("deleteWebhook", cancellationToken);
    }

    public async Task<Update[]> GetUpdates(UpdateParams payload, CancellationToken cancellationToken)
    {
        return await Post<UpdateParams, Update[]>("getUpdates", payload, cancellationToken);
    }

    public async Task<Message> SendMessage(SendMessageParams payload, CancellationToken cancellationToken)
    {
        return await Post<SendMessageParams, Message>("sendMessage", payload, cancellationToken);
    }

    async Task<T> Get<T>(string method, CancellationToken cancellationToken)
    {
        var responseMessage = await Client.GetAsync(method, cancellationToken);
        return await DeserializeResult<T>(responseMessage, cancellationToken);
    }

    async Task<TResult> Post<TPayload, TResult>(string method, TPayload payload, CancellationToken cancellationToken)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");
        return await Post<TResult>(method, content, cancellationToken);
    }

    async Task<TResult> Post<TResult>(string method, HttpContent content, CancellationToken cancellationToken)
    {
        var responseMessage = await Client.PostAsync(method, content, cancellationToken);
        return await DeserializeResult<TResult>(responseMessage, cancellationToken);
    }

    async Task Post(string method, CancellationToken cancellationToken)
    {
        var responseMessage = await Client.PostAsync(method, null, cancellationToken);
        var result = await Deserialize<TelegramEmptyResponse>(responseMessage, cancellationToken);

        EnsureOk(result);
    }

    async Task<T> DeserializeResult<T>(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
    {
        var response = await Deserialize<TelegramResponse<T>>(responseMessage, cancellationToken);
        EnsureOk(response);
        return response.result;
    }

    async Task<T> Deserialize<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();

        var responseStream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(responseStream, null, cancellationToken);
    }

    void EnsureOk(TelegramEmptyResponse response)
    {
        if (response.ok)
            return;

        var errMsg = !String.IsNullOrWhiteSpace(response.description)
            ? response.description
            : "No description provided";

        throw new TelegramException(errMsg);
    }
}

#pragma warning disable IDE1006 // Naming Styles

class TelegramEmptyResponse
{
    public bool ok { get; set; }
    public string description { get; set; }
}

class TelegramResponse<T> : TelegramEmptyResponse
{
    public T result { get; set; }
}

class TelegramException : Exception
{
    public TelegramException(String description) : base(message: description)
    {
    }
}

class User
{
    public int id { get; set; }
    public bool is_bot { get; set; }
    public string first_name { get; set; }
    public string last_name { get; set; }
    public string username { get; set; }
    public string language_code { get; set; }
}

class WebhookInfo
{
    public string url { get; set; }
    public bool has_custom_certificate { get; set; }
    public int pending_update_count { get; set; }
    public int? last_error_date { get; set; }
    public int? last_error_message { get; set; }
    public int? max_connections { get; set; }
    public string[] allowed_updates { get; set; }
}

class UpdateParams
{
    public int? offset { get; set; }
    public int? limit { get; set; }
    public int? timeout { get; set; }
    public string[] allowed_updates { get; set; }
}

class Update
{
    public int update_id { get; set; }
    public Message message { get; set; }
}

class SendMessageParams
{
    public long chat_id { get; set; }
    public string text { get; set; }
    public ReplyKeyboardMarkup reply_markup { get; set; }
}

class ReplyKeyboardMarkup
{
    public KeyboardButton[][] keyboard { get; set; }
    public bool one_time_keyboard { get; set; }
}

class KeyboardButton
{
    public string text { get; set; }
}

class Message
{
    public int message_id { get; set; }
    public User from { get; set; }
    public int date { get; set; }
    public Chat chat { get; set; }
    public string text { get; set; }
}

class Chat
{
    public long id { get; set; }
    public string type { get; set; }
}

#pragma warning restore IDE1006 // Naming Styles