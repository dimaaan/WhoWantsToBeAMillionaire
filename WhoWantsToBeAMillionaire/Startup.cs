using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        var telegramOptions = Configuration.GetSection("Telegram").Get<TelegramOptions>();
        services.AddSingleton(telegramOptions);

        services.AddSingleton(LoadTexts<Speech>("Millionaire.speech.json"));
        services.AddSingleton(LoadTexts<Question[][]>("Millionaire.questions.json"));
        services.AddHttpClient<BotApiClient>(c => c.BaseAddress = new Uri($@"https://api.telegram.org/bot{telegramOptions.ApiKey}/"));
        services.AddSingleton(provider => new StateSerializer(
            Environment.IsDevelopment() ? "./state.json" : "/var/tmp/millionaire/state.json",
            provider.GetService<ILogger<StateSerializer>>()
        ));
        services.AddSingleton<Narrator>();
        services.AddSingleton<Game>();
        services.AddSingleton(Configuration.GetSection("Mongo").Get<MongoOptions>());
        services.AddSingleton<EventLogger>();

        if (Environment.IsDevelopment())
        {
            services.AddHostedService<UpdatesPoller>();
        }

        static T LoadTexts<T>(string resourceName)
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            return JsonSerializer.DeserializeAsync<T>(stream).Result;
        }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(
        IApplicationBuilder app,
        IHostApplicationLifetime lifetime,
        BotApiClient tg,
        ILogger<Startup> logger,
        Game gameService,
        TelegramOptions telegramOptions
    )
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost(new UriBuilder(telegramOptions.WebhookAddress).Path, async context =>
            {
                Update update;

                try
                {
                    update = await JsonSerializer.DeserializeAsync<Update>(context.Request.Body, null, context.RequestAborted);
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Failed deserialize request body\nContent type: {ContentType}", context.Request.ContentType);
                    context.Response.StatusCode = 400;
                    return;
                }

                await gameService.UpdateGame(update, context.RequestAborted);
                context.Response.StatusCode = 200;
            });

            endpoints.MapGet("/", async context => {
                await context.Response.WriteAsync($"Games: {gameService.GamesCount()}");
            });
        });

        if (!Environment.IsDevelopment())
        {
            lifetime.ApplicationStarted.Register(() => SetWebHook(tg, logger, lifetime.ApplicationStopping, telegramOptions));
            lifetime.ApplicationStopping.Register(() => RemoveWebHook(tg, logger, lifetime.ApplicationStopped));
        }

        var user = tg.GetMeAsync(lifetime.ApplicationStopping).Result;
        logger.LogInformation("Working as {User}", user.username);
    }

    void SetWebHook(BotApiClient tg, ILogger<Startup> logger, CancellationToken cancellationToken, TelegramOptions telegramOptions)
    {
        var webHookInfo = tg.GetWebhookInfoAsync(cancellationToken).Result;
        if (!String.IsNullOrWhiteSpace(webHookInfo.last_error_message))
            logger.LogInformation("Tegeram last error at {Date}: {Msg}", webHookInfo.last_error_date, webHookInfo.last_error_message);

        if (!String.IsNullOrWhiteSpace(webHookInfo.url))
            logger.LogWarning("Tegeram webhook already set to {Url}. Overriding...", webHookInfo.url);

        if (String.IsNullOrWhiteSpace(telegramOptions.Certificate))
            throw new Exception("Path to telegram certificate is required for non developer environment");

        tg.SetWebHookAsync(telegramOptions.WebhookAddress, telegramOptions.Certificate, cancellationToken).Wait();
        logger.LogInformation("Webhook set: {Url}", telegramOptions.WebhookAddress);
    }

    void RemoveWebHook(BotApiClient tg, ILogger<Startup> logger, CancellationToken cancellationToken)
    {
        tg.DeleteWebhookAsync(cancellationToken).Wait();
        logger.LogInformation("Webhook removed");
    }
}
