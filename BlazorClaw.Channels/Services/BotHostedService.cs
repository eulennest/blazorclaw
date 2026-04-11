using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Channels.Services
{
    public class BotHostedService<TConfig, TBot>(IOptionsMonitor<BotConfigs<TConfig>> botConfigs, ChannelRegistry channels, ILogger<BotHostedService<TConfig, TBot>> logger, IServiceScopeFactory serviceScopeFactory) : IHostedService where TConfig : BotEntry where TBot : IChannelBot
    {
        private readonly IServiceScope scope = serviceScopeFactory.CreateScope();

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var config in botConfigs.CurrentValue)
            {
                if (!config.Value.Enabled) continue;

                logger.LogInformation("{type} '{id}' initializing ...", typeof(TBot).Name, config.Key);

                try
                {
                    var bot = ActivatorUtilities.CreateInstance<TBot>(scope.ServiceProvider);
                    if (bot == null)
                    {
                        logger.LogError("Bot '{id}' can't created.", config.Key);
                        continue;

                    }
                    if (bot is IConfigure<TConfig> configurable)
                    {
                        if (!await configurable.ConfigureAsync(config.Value))
                        {
                            logger.LogWarning("Bot '{id}' configuration failed.", config.Key);
                            continue;
                        }
                    }

                    await bot.StartAsync(cancellationToken);
                    channels.Add(bot);
                    logger.LogInformation("Bot '{id}' started successfully.", config.Key);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start Bot '{id}'", config.Key);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var item in channels.OfType<TBot>().ToList())
            {
                try
                {
                    await item.StopAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error stopping Matrix client: {Message}", ex.Message);
                }
                finally
                {
                    channels.Remove(item);
                }
            }
        }
    }

}
