using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BlazorClaw.Channels.Services
{
    public class BotHostedService<TConfig, TBot>(IOptionsMonitor<BotConfigs<TConfig>> botConfigs, ChannelRegistry channels, ILogger<BotHostedService<TConfig, TBot>> logger, IServiceScopeFactory serviceScopeFactory) : IHostedService where TConfig : BotEntry where TBot : class, IChannelBot
    {
        private readonly IServiceScope scope = serviceScopeFactory.CreateScope();
        private readonly Dictionary<string, TBot> bots = [];
        private readonly Dictionary<string, TConfig> activeConfigs = [];
        private readonly SemaphoreSlim reloadLock = new(1, 1);
        private IDisposable? changeSubscription;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            changeSubscription = botConfigs.OnChange(newConfig => _ = ReloadBotsAsync(newConfig));
            await LoadBotsAsync(botConfigs.CurrentValue, cancellationToken);
        }

        private async Task LoadBotsAsync(BotConfigs<TConfig> config, CancellationToken cancellationToken)
        {
            foreach (var item in config)
            {
                if (!item.Value.Enabled) continue;
                await AddBotAsync(item.Key, item.Value, cancellationToken);
            }
        }

        private async Task ReloadBotsAsync(BotConfigs<TConfig> newConfig)
        {
            await reloadLock.WaitAsync();
            try
            {
                logger.LogInformation("{type} config changed, reloading bots...", typeof(TBot).Name);

                var currentIds = bots.Keys.ToList();
                var newIds = newConfig.Keys.ToList();
                var removedIds = currentIds.Except(newIds).ToList();

                foreach (var botId in removedIds)
                {
                    await RemoveBotAsync(botId, CancellationToken.None);
                }

                foreach (var item in newConfig)
                {
                    if (!item.Value.Enabled)
                    {
                        if (bots.ContainsKey(item.Key))
                        {
                            await RemoveBotAsync(item.Key, CancellationToken.None);
                        }
                        continue;
                    }

                    if (!bots.ContainsKey(item.Key))
                    {
                        await AddBotAsync(item.Key, item.Value, CancellationToken.None);
                        continue;
                    }

                    if (!activeConfigs.TryGetValue(item.Key, out var existingConfig) || !ConfigEquals(existingConfig, item.Value))
                    {
                        await RemoveBotAsync(item.Key, CancellationToken.None);
                        await AddBotAsync(item.Key, item.Value, CancellationToken.None);
                    }
                }
            }
            finally
            {
                reloadLock.Release();
            }
        }

        private async Task AddBotAsync(string botId, TConfig config, CancellationToken cancellationToken)
        {
            logger.LogInformation("{type} '{id}' initializing ...", typeof(TBot).Name, botId);

            try
            {
                var bot = ActivatorUtilities.CreateInstance<TBot>(scope.ServiceProvider);
                if (bot == null)
                {
                    logger.LogError("Bot '{id}' can't created.", botId);
                    return;
                }

                if (bot is not IKeyedConfigure<TConfig> keyedConfigurable)
                {
                    logger.LogError("Bot '{id}' does not implement keyed configuration.", botId);
                    return;
                }

                var configured = await keyedConfigurable.ConfigureAsync(botId, config);
                if (!configured)
                {
                    logger.LogWarning("Bot '{id}' configuration failed.", botId);
                    return;
                }

                await bot.StartAsync(cancellationToken);
                bots[botId] = bot;
                activeConfigs[botId] = (TConfig)config.Clone();
                channels.Add(bot);
                logger.LogInformation("Bot '{id}' started successfully.", botId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start Bot '{id}'", botId);
            }
        }

        private async Task RemoveBotAsync(string botId, CancellationToken cancellationToken)
        {
            if (!bots.TryGetValue(botId, out var bot)) return;

            try
            {
                await bot.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping Bot '{id}': {Message}", botId, ex.Message);
            }
            finally
            {
                channels.Remove(bot);
                bots.Remove(botId);
                activeConfigs.Remove(botId);
            }
        }

        private static bool ConfigEquals(TConfig left, TConfig right)
        {
            return JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            changeSubscription?.Dispose();

            foreach (var item in bots.Keys.ToList())
            {
                await RemoveBotAsync(item, cancellationToken);
            }

            scope.Dispose();
            reloadLock.Dispose();
        }
    }
}
