using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;
using System.Threading.Tasks;

namespace BotApi
{
    class WebHookMiddleware
    {
        readonly Game GameService;

        public WebHookMiddleware(RequestDelegate _, Game gameService)
        {
            GameService = gameService;
        }

        public async Task Invoke(HttpContext context)
        {
            var update = await JsonSerializer.DeserializeAsync<Update>(context.Request.Body, null, context.RequestAborted);
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