using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

class UpdatesPollingService : BackgroundService
{
    readonly ILogger<UpdatesPollingService> Logger;
    readonly BotApiClient BotApi;
    readonly GameService GameService;

    public UpdatesPollingService(BotApiClient botApi, GameService gameService, ILogger<UpdatesPollingService> logger)
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
            await BotApi.DeleteWebhook(stoppingToken);

            var request = new UpdateParams
            {
                offset = 0,
                timeout = 1,
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                var updates = await BotApi.GetUpdates(request, stoppingToken);

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