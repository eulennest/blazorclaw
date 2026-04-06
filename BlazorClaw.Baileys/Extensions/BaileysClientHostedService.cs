using Microsoft.Extensions.Hosting;

namespace Baileys.Extensions;

/// <summary>
/// A background service that automatically initiates the Baileys connection
/// when the application starts.
/// </summary>
public sealed class BaileysClientHostedService(
    BaileysClient client,
    Utils.ILogger logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.Info("Baileys background service starting...");
            await client.ConnectAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.Error($"Failed to start Baileys client: {ex.Message}");
            logger.Exception(ex);
        }
    }
}
