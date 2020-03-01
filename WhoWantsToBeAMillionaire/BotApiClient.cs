using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class BotApiClient
{
    readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        IgnoreNullValues = true
    };
    readonly HttpClient HttpClient;

    public BotApiClient(HttpClient client)
    {
        HttpClient = client;
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
            throw new BotApiException("Setting webhook failed");
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
        var responseMessage = await HttpClient.GetAsync(method, cancellationToken);
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
        var responseMessage = await HttpClient.PostAsync(method, content, cancellationToken);
        return await DeserializeResultAsync<TResult>(responseMessage, cancellationToken);
    }

    async Task PostAsync(string method, CancellationToken cancellationToken)
    {
        var responseMessage = await HttpClient.PostAsync(method, null, cancellationToken);
        var result = await DeserializeAsync<BotApiEmptyResponse>(responseMessage, cancellationToken);

        EnsureOk(result);
    }

    async Task<T> DeserializeResultAsync<T>(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
    {
        var response = await DeserializeAsync<BotApiResponse<T>>(responseMessage, cancellationToken);
        EnsureOk(response);
        return response.result;
    }

    async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var responseStream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(responseStream, null, cancellationToken);
    }

    void EnsureOk(BotApiEmptyResponse response)
    {
        if (response.ok)
            return;

        var errMsg = !String.IsNullOrWhiteSpace(response.description)
            ? response.description
            : "No description provided";

        throw new BotApiException(errMsg, response.error_code);
    }
}
