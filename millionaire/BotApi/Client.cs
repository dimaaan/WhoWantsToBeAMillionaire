using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Json;

namespace BotApi
{
    public class Client
    {
        readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            IgnoreNullValues = true
        };
        readonly HttpClient HttpClient;

        public Client(HttpClient client)
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
                throw new Exception("Setting webhook failed");
        }

        public async Task DeleteWebhookAsync(CancellationToken cancellationToken)
        {
            var responseMessage = await HttpClient.GetAsync("deleteWebhook", cancellationToken);
            await DeserializeAsync<BotApiEmptyResponse>(responseMessage, cancellationToken);
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
            var response = await DeserializeAsync<BotApiResponse<T>>(responseMessage, cancellationToken);
            return response.result;
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
            var response = await DeserializeAsync<BotApiResponse<TResult>>(responseMessage, cancellationToken);
            return response.result;
        }

        static async Task<T> DeserializeAsync<T>(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
            where T : BotApiEmptyResponse
        {
            var response = await responseMessage.Content.ReadFromJsonAsync<T>(null, cancellationToken)
                ?? throw new Exception("Unexpected empty response");

            if (response.ok)
                return response;

            var errMsg = !String.IsNullOrWhiteSpace(response.description)
               ? response.description
               : "No description provided";

            throw response.error_code switch
            {
                429 => new TooManyRequestsException(
                    description: errMsg,
                    code: response.error_code,
                    retryAfter: responseMessage.Headers.RetryAfter?.Delta
                ),
                _ => new BotApiException(
                  description: errMsg,
                  code: response.error_code
              ),
            };
        }
    }
}
