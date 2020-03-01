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

        var telegramOptions = Configuration.GetSection("Telegram").Get<TelegramOptions>();
        services.AddSingleton(telegramOptions);

        services.AddSingleton(LoadTexts<Speech>("Millionaire.speech.json"));
        services.AddSingleton(LoadTexts<Question[][]>("Millionaire.questions.json"));
        services.AddHttpClient<BotApi.Client>(c => c.BaseAddress = new Uri($@"https://api.telegram.org/bot{telegramOptions.ApiKey}/"));
        services.AddSingleton(provider => new StateSerializer(
            Environment.IsDevelopment() ?
                "./state.json" :
                "/var/tmp/millionaire/state.json", // there is no universal way to get tmp folder on UNIX, but this, at least, works for Ubuntu
            provider.GetService<ILogger<StateSerializer>>()
        ));
        services.AddSingleton<Narrator>();
        services.AddSingleton<Game>();
        services.AddSingleton(Configuration.GetSection("Mongo").Get<MongoOptions>());
        services.AddSingleton<EventLogger>();

        if (Environment.IsDevelopment())
        {
            services.AddHostedService<BotApi.UpdatesPoller>();
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
        BotApi.Client botApi,
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

        if (!Environment.IsDevelopment())
        {
            lifetime.ApplicationStarted.Register(() => SetWebHook(botApi, logger, lifetime.ApplicationStopping, telegramOptions));
            lifetime.ApplicationStopping.Register(() => RemoveWebHook(botApi, logger, lifetime.ApplicationStopped));
        }

        var user = botApi.GetMeAsync(lifetime.ApplicationStopping).Result;
        logger.LogInformation("Working as {User}", user.username);
    }

    void SetWebHook(BotApi.Client botApi, ILogger<Startup> logger, CancellationToken cancellationToken, TelegramOptions telegramOptions)
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

        if (String.IsNullOrWhiteSpace(telegramOptions.Certificate))
            throw new Exception("Path to telegram certificate is required for non developer environment");

        botApi.SetWebHookAsync(telegramOptions.WebhookAddress, telegramOptions.Certificate, cancellationToken).Wait();
        logger.LogInformation("Webhook set: {Url}", telegramOptions.WebhookAddress);
    }

    void RemoveWebHook(BotApi.Client botApi, ILogger<Startup> logger, CancellationToken cancellationToken)
    {
        botApi.DeleteWebhookAsync(cancellationToken).Wait();
        logger.LogInformation("Webhook removed");
    }
}
