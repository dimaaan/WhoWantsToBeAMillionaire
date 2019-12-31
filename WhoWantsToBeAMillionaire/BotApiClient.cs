using System;
using System.Collections.Generic;
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

    public async Task<User> GetMeAsync(CancellationToken cancellationToken)
    {
        return await GetAsync<User>("getMe", cancellationToken);
    }

    public async Task<WebhookInfo> GetWebhookInfoAsync(CancellationToken cancellationToken)
    {
        return await GetAsync<WebhookInfo>("getWebhookInfo", cancellationToken);
    }

    public async Task SetWebHookAsync(string uri, string certificatePath, CancellationToken cancellationToken)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(uri), "url" },
            { new ByteArrayContent(File.ReadAllBytes(certificatePath)), "certificate", certificatePath }
        };

        var result = await PostAsync<bool>("setWebhook", content, cancellationToken);

        if (!result)
            throw new TelegramException("Setting webhook failed");
    }

    public async Task DeleteWebhookAsync(CancellationToken cancellationToken)
    {
        await PostAsync("deleteWebhook", cancellationToken);
    }

    public async Task<Update[]> GetUpdatesAsync(UpdateParams payload, CancellationToken cancellationToken)
    {
        return await PostAsync<UpdateParams, Update[]>("getUpdates", payload, cancellationToken);
    }

    public async Task<Message> SendMessageAsync(SendMessageParams payload, CancellationToken cancellationToken)
    {
        return await PostAsync<SendMessageParams, Message>("sendMessage", payload, cancellationToken);
    }

    async Task<T> GetAsync<T>(string method, CancellationToken cancellationToken)
    {
        var responseMessage = await Client.GetAsync(method, cancellationToken);
        return await DeserializeResultAsync<T>(responseMessage, cancellationToken);
    }

    async Task<TResult> PostAsync<TPayload, TResult>(string method, TPayload payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await PostAsync<TResult>(method, content, cancellationToken);
    }

    async Task<TResult> PostAsync<TResult>(string method, HttpContent content, CancellationToken cancellationToken)
    {
        var responseMessage = await Client.PostAsync(method, content, cancellationToken);
        return await DeserializeResultAsync<TResult>(responseMessage, cancellationToken);
    }

    async Task PostAsync(string method, CancellationToken cancellationToken)
    {
        var responseMessage = await Client.PostAsync(method, null, cancellationToken);
        var result = await DeserializeAsync<TelegramEmptyResponse>(responseMessage, cancellationToken);

        EnsureOk(result);
    }

    async Task<T> DeserializeResultAsync<T>(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
    {
        var response = await DeserializeAsync<TelegramResponse<T>>(responseMessage, cancellationToken);
        EnsureOk(response);
        return response.result;
    }

    async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
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
    public string? description { get; set; }
}

class TelegramResponse<T> : TelegramEmptyResponse
{
    public T result { get; set; } = default!;
}

class TelegramException : Exception
{
    public TelegramException(String description) : base(message: description)
    {
    }
}

class WebhookInfo
{
    public string url { get; set; } = default!;
    public bool has_custom_certificate { get; set; }
    public int pending_update_count { get; set; }
    public int? last_error_date { get; set; }
    public int? last_error_message { get; set; }
    public int? max_connections { get; set; }
    public string[]? allowed_updates { get; set; }
}

class UpdateParams
{
    public int? offset { get; set; }
    public int? limit { get; set; }
    public int? timeout { get; set; }
    public string[]? allowed_updates { get; set; }
}

class Update
{
    public int update_id { get; set; }
    public Message? message { get; set; }
}

class SendMessageParams
{
    public long chat_id { get; set; }
    public string text { get; set; } = default!;
    public ReplyKeyboardMarkup? reply_markup { get; set; }
}

class Message
{
    public int message_id { get; set; }
    public User from { get; set; } = default!;
    public int date { get; set; }
    public Chat chat { get; set; } = default!;
    public string? text { get; set; }
}

class User
{
    public int id { get; set; }
    public bool is_bot { get; set; }
    public string first_name { get; set; } = default!;
    public string? last_name { get; set; }
    public string? username { get; set; }
    public string? language_code { get; set; }
}

class Chat
{
    public long id { get; set; }
    public string type { get; set; } = default!;
}

class ReplyKeyboardMarkup
{
    public IEnumerable<IEnumerable<KeyboardButton>> keyboard { get; set; } = default!;
    public bool? one_time_keyboard { get; set; }
}

class KeyboardButton
{
    public string text { get; set; } = default!;
}

#pragma warning restore IDE1006 // Naming Styles