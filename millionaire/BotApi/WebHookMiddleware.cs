using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;

namespace BotApi
{
    class WebHookMiddleware
    {
        readonly Game GameService;
        readonly ILogger<WebHookMiddleware> Logger;

        public WebHookMiddleware(RequestDelegate _, Game gameService, ILogger<WebHookMiddleware> logger)
        {
            GameService = gameService;
            Logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var update = await JsonSerializer.DeserializeAsync<Update>(context.Request.Body, null, context.RequestAborted);

            if (update == null)
            {
                Logger.LogWarning("Body of the incoming HTTP request is empty. Ignoring webhook");
                return;
            }

            await GameService.UpdateGame(update, context.RequestAborted);
            context.Response.StatusCode = 200;
        }
    }
}

static class BotApiWebHookMiddlewareExtensions
{
    public static IEndpointConventionBuilder MapWebHook(this IEndpointRouteBuilder endpoints, string pattern)
    {
        var pipeline = endpoints.CreateApplicationBuilder()
            .UseMiddleware<BotApi.WebHookMiddleware>()
            .Build();

        return endpoints.MapPost(pattern, pipeline);
    }
}