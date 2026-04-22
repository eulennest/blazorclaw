using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace BlazorClaw.Server.Services
{
    public class WebChatChannelBot : AbstractChannelBot
    {
        private readonly IHubContext<ChatHub> hub;
        private readonly ILogger<WebChatChannelBot> logger;

        public WebChatChannelBot(IHubContext<ChatHub> hub, ILogger<WebChatChannelBot> logger) : base("WebChat")
        {
            BotId = "default";
            this.hub = hub;
            this.logger = logger;
        }

        public override Task SendChannelAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            return SendUserAsync(channelId, message, cancellationToken);
        }

        public override Task SendUserAsync(IChannelSession channelId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            if (channelId.SessionId == Guid.Empty && Guid.TryParse(channelId.ChannelId, out var sess)) channelId.SessionId = sess;
            logger.LogInformation("Sending message to channel {ChannelId} in session {SessionId}", channelId.ChannelId, channelId.SessionId);
            return hub.Clients.Group(channelId.SessionId.ToString()).SendAsync("ReceiveMessage", channelId.SessionId, message, cancellationToken: cancellationToken);
        }

        public override Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task HandelIncomingAsync(Guid session, string senderId, object message, CancellationToken cancellationToken = default)
        {
            OnMessageReceived(new ChannelSession(this, session.ToString(), senderId) { SessionId = session }, message);
            return Task.CompletedTask;
        }
    }

    public class WebChatBotHostedService(WebChatChannelBot bot, ChannelRegistry channels) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            channels.Add(bot);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            channels.Remove(bot);
            return Task.CompletedTask;
        }
    }
}