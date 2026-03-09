using BlazorClaw.Core.Sessions;
using Matrix.Sdk;
using Matrix.Sdk.Core.Domain.RoomEvent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlazorClaw.Channels.Services
{
    public class MatrixBotHostedService(IConfiguration configuration, ILogger<MatrixBotHostedService> logger, IMessageDispatcher messageDispatcher) : IHostedService
    {
        private readonly List<MatrixChannelBot> _bots = [];

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var matrixConfigs = configuration.GetSection("Channels:Matrix").GetChildren();
            var factory = new MatrixClientFactory();

            foreach (var config in matrixConfigs)
            {
                var homeserver = new Uri(config["Homeserver"] ?? "https://matrix.org");
                var userId = config["UserId"] ?? "";
                var password = config["Password"] ?? "";

                logger.LogInformation("Matrix Bot '{id}' initializing ...", config.Key);

                try
                {
                    IMatrixClient client = factory.Create();
                    await client.LoginAsync(homeserver, userId, password, "BlazorClawBot");

                    client.OnMatrixRoomEventsReceived += HandleUpdate;
                    client.Start();

                    var bot = new MatrixChannelBot(client);
                    messageDispatcher.Register(bot);
                    _bots.Add(bot);
                    logger.LogInformation("Matrix Bot '{id}' started successfully.", config.Key);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to start Matrix Bot '{id}'", config.Key);
                }
            }
        }

        private async void HandleUpdate(object? sender, MatrixRoomEventsEventArgs eventArgs)
        {
            if (sender is not IMatrixClient client) return;
            foreach (var roomEvent in eventArgs.MatrixRoomEvents)
            {
                if (client.UserId != roomEvent.SenderUserId)
                {
                    if (roomEvent is TextMessageEvent textMessageEvent)
                    {
                        logger.LogInformation("Matrix received message in {RoomId}", roomEvent.RoomId);
                        await ProcessMatrixMessage(client, roomEvent.RoomId, roomEvent.SenderUserId, textMessageEvent.Message);
                    }
                }
            }
        }

        private async Task ProcessMatrixMessage(IMatrixClient client, string roomId, string senderUserId, string message)
        {
            try
            {
                var inst = _bots.FirstOrDefault(b => b.Client == client);
                if (inst == null || message == null) return;
                await inst.Client.SendTypingSignal(roomId, true);
                await inst.OnMessageReceivedAsync(new ChannelSession(inst, roomId, senderUserId), message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error: {Messsage}", ex.Message);
            }
            finally
            {
                await client.SendTypingSignal(roomId, false);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var item in _bots)
            {
                try
                {
                    messageDispatcher.Unregister(item);
                    item.Client.Stop();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error stopping Matrix client: {Message}", ex.Message);
                }
            }
            return Task.CompletedTask;
        }
    }

    public class MatrixChannelBot(IMatrixClient Client) : AbstractChannelBot("Matrix")
    {
        internal IMatrixClient Client { get; } = Client;
        public override Task SendMessageAsync(IChannelSession channelId, object message, CancellationToken cancellationToken = default)
        {
            return Client.SendMessageAsync(channelId.ChannelId, Convert.ToString(message) ?? string.Empty);
        }
    }
}
