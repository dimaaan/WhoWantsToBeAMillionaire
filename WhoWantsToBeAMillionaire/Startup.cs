using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;

class Startup
{
    readonly IConfiguration Configuration;
    readonly IHostEnvironment Environment;

    public Startup(IConfiguration configuration, IHostEnvironment environment)
    {
        Configuration = configuration;
        Environment = environment;
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(LoadStrings());
        services.AddHttpClient<BotApiClient>(c => {
            c.BaseAddress = new Uri($@"https://api.telegram.org/bot{Configuration["Telegram:ApiKey"]}/");
        });
        services.AddSingleton<GameService>();

        if (Environment.IsDevelopment())
        {
            services.AddHostedService<UpdatesPollingService>();
        }

        static Strings LoadStrings()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var speechStream = asm.GetManifestResourceStream("Millionaire.speech.json");
            using var questionsStream = asm.GetManifestResourceStream("Millionaire.questions.json");
            return new Strings
            {
                Speech = JsonSerializer.DeserializeAsync<Speech>(speechStream).Result,
                Questions = JsonSerializer.DeserializeAsync<Question[][]>(questionsStream).Result,
            };
        }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostApplicationLifetime lifetime, BotApiClient tg, ILogger<Startup> logger, GameService gameService)
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost("/{key}", async context =>
            {
                try
                {
                    var update = await JsonSerializer.DeserializeAsync<Update>(context.Request.Body, null, context.RequestAborted);
                    await gameService.UpdateGame(update, context.RequestAborted);
                    context.Response.StatusCode = 200;
                }
                catch(JsonException ex)
                {
                    logger.LogError(ex, "Failed deserialize request body. content type: {0}", context.Request.ContentType);
                    context.Response.StatusCode = 400;
                }
            });
        });

        if(!Environment.IsDevelopment())
        {
            lifetime.ApplicationStarted.Register(() => SetWebHook(tg, logger, lifetime.ApplicationStopping));
            lifetime.ApplicationStopping.Register(() => RemoveWebHook(tg, logger, lifetime.ApplicationStopped));
        }

        var user = tg.GetMe(lifetime.ApplicationStopping).Result;
        logger.LogInformation("Working as {0}", user.username);
    }

    void SetWebHook(BotApiClient tg, ILogger<Startup> logger, CancellationToken cancellationToken)
    {
        var webHookInfo = tg.GetWebhookInfo(cancellationToken).Result;
        if (!String.IsNullOrWhiteSpace(webHookInfo.url))
            logger.LogWarning("Tegeram webhook already set to {0}. Overriding...", webHookInfo.url);

        var webHookaddress = Configuration["Telegram:WebhookAddress"];
        tg.SetWebHook(webHookaddress, Configuration["Telegram:Certificate"], cancellationToken).Wait();
        logger.LogInformation("Webhook set: {0}", webHookaddress);
    }

    void RemoveWebHook(BotApiClient tg, ILogger<Startup> logger, CancellationToken cancellationToken)
    {
        tg.DeleteWebhook(cancellationToken).Wait();
        logger.LogInformation("Webhook removed");
    }
}
