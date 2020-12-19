using Events;
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
        services.AddRazorPages();

        var telegramOptions = Configuration.GetSection("Telegram").Get<TelegramOptions?>()
            ?? throw new Exception("Telegram configuration not found");
        services.AddSingleton(telegramOptions);

        services.AddSingleton(LoadTexts<Speech>("millionaire.speech.json"));
        services.AddSingleton(LoadTexts<Question[][]>("millionaire.questions.json"));
        services.AddHttpClient<BotApi.IClient, BotApi.Client>(c => c.BaseAddress = new Uri($@"https://api.telegram.org/bot{telegramOptions.ApiKey}/"));
        services.AddSingleton<ISessions, Sessions>();
        services.AddSingleton<INarrator, Narrator>();
        services.AddSingleton<Game>();
        services.AddSingleton(Configuration.GetSection("Sqlite").Get<SqliteOptions?>()
            ?? throw new Exception("Sqlite configuration not found"));
        services.AddSingleton<EventLogger>();

        if (Environment.IsDevelopment())
        {
            services.AddHostedService<BotApi.UpdatesPoller>();
        }

        static T LoadTexts<T>(string resourceName)
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new Exception($"Embdeded resource {resourceName} not found");

            return JsonSerializer.Deserialize<T>(new System.IO.StreamReader(stream).ReadToEnd())
                ?? throw new Exception($"Embdeded resource {resourceName} is empty");
        }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(
        IApplicationBuilder app,
        IHostApplicationLifetime lifetime,
        BotApi.IClient botApi,
        ILogger<Startup> logger,
        TelegramOptions telegramOptions
    )
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapWebHook(new UriBuilder(telegramOptions.WebhookAddress).Path);
            endpoints.MapRazorPages();
        });

        app.UseStaticFiles();

        if (!Environment.IsDevelopment())
        {
            lifetime.ApplicationStarted.Register(() => SetWebHook(botApi, logger, telegramOptions, lifetime.ApplicationStopping));
            lifetime.ApplicationStopping.Register(() => RemoveWebHook(botApi, logger, lifetime.ApplicationStopped));
        }

        var user = botApi.GetMeAsync(lifetime.ApplicationStopping).Result;
        logger.LogInformation("Working as {User}", user.username);
    }

    static void SetWebHook(BotApi.IClient botApi, ILogger<Startup> logger, TelegramOptions telegramOptions, CancellationToken cancellationToken)
    {
        var webHookInfo = botApi.GetWebhookInfoAsync(cancellationToken).Result;
        if (!String.IsNullOrWhiteSpace(webHookInfo.last_error_message))
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(webHookInfo.last_error_date ?? 0)
                .ToLocalTime()
                .ToString(System.Globalization.CultureInfo.GetCultureInfo("ru-ru"));
            logger.LogInformation("Tegeram last error at {Date}: {Msg}", date, webHookInfo.last_error_message);
        }

        if (!String.IsNullOrWhiteSpace(webHookInfo.url))
            logger.LogWarning("Tegeram webhook already set to {Url}. Overriding...", webHookInfo.url);

        botApi.SetWebHookAsync(telegramOptions.WebhookAddress, telegramOptions.Certificate, cancellationToken).Wait(cancellationToken);
        logger.LogInformation("Webhook set: {Url}", telegramOptions.WebhookAddress);
    }

    static void RemoveWebHook(BotApi.IClient botApi, ILogger<Startup> logger, CancellationToken cancellationToken)
    {
        botApi.DeleteWebhookAsync(cancellationToken).Wait(cancellationToken);
        logger.LogInformation("Webhook removed");
    }
}
