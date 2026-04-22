using BlazorClaw.Core.Sessions;
using Matrix.Sdk;
using Matrix.Sdk.Core.Domain.RoomEvent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace BlazorClaw.Channels.Services
{

    public class MatrixChannelBot(MatrixClientFactory factory, ILogger<MatrixChannelBot> logger) : AbstractConfigChannelBot<MatrixBotEntry>("Matrix")
    {
        internal IMatrixClient? Client { get; private set; }
        public override Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return Client?.SendMessageAsync(channelId.ChannelId, message.Text) ?? Task.CompletedTask;
        }

        public override Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return Client?.SendMessageAsync(channelId.ChannelId, message.Text) ?? Task.CompletedTask;
        }

        public override async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (Client == null || Config == null) throw new InvalidOperationException("Not configured");
            var homeserver = new Uri(Config.Homeserver ?? "https://matrix.org", UriKind.Absolute);
            var userId = Config.UserId;
            var password = Config.Password;
            await Client.LoginAsync(homeserver, userId, password, "BlazorClawBot");
            Client.Start();
        }

        public override Task StopAsync(CancellationToken cancellationToken = default)
        {
            Client?.Stop();
            return Task.CompletedTask;
        }

        protected override ValueTask<bool> ConfigureAsync()
        {
            if (Client != null) Client.OnMatrixRoomEventsReceived -= HandleUpdate;
            Client = factory.Create();
            Client.OnMatrixRoomEventsReceived += HandleUpdate;
            return ValueTask.FromResult(true);
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
                        await ProcessMatrixMessage(roomEvent.RoomId, roomEvent.SenderUserId, textMessageEvent.Message);
                    }
                }
            }
        }

        private async Task ProcessMatrixMessage(string roomId, string senderUserId, string message)
        {
            try
            {
                await Client!.SendTypingSignal(roomId, true);
                OnMessageReceived(new ChannelSession(this, roomId, senderUserId), message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error: {Messsage}", ex.Message);
            }
            finally
            {
                await Client!.SendTypingSignal(roomId, false);
            }
        }
    }
    public class MatrixBotEntry : BotEntry
    {
        public string Homeserver { get; set; } = "https://matrix.org";
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

}
