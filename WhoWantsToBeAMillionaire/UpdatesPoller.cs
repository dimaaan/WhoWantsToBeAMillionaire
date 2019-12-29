using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

class UpdatesPoller : BackgroundService
{
    readonly ILogger<UpdatesPoller> Logger;
    readonly BotApiClient BotApi;
    readonly Game GameService;

    public UpdatesPoller(BotApiClient botApi, Game gameService, ILogger<UpdatesPoller> logger)
    {
        Logger = logger;
        BotApi = botApi;
        GameService = gameService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Starting...");

        try
        {
            await BotApi.DeleteWebhookAsync(stoppingToken);

            var request = new UpdateParams
            {
                offset = 0,
                timeout = 1,
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                var updates = await BotApi.GetUpdatesAsync(request, stoppingToken);

                foreach (var update in updates)
                {
                    await GameService.UpdateGame(update, stoppingToken);
                    request.offset = update.update_id + 1;
                }
            }
        }
        catch (TaskCanceledException)
        {
        }

        Logger.LogInformation("Stopped");
    }
}