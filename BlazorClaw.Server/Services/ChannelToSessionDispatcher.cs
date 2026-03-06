using BlazorClaw.Server.Models;

namespace BlazorClaw.Server.Services
{
    public interface IChannelToSessionDispatcher
    {
        /// <summary>
        /// Gibt die SessionId zurück, die zu dieser User-Kanal-Kombination gehört.
        /// </summary>
        Task<Guid> GetOrCreateSessionAsync(string userId, string channelInstanceId);
    }

    public class ChannelToSessionDispatcher : IChannelToSessionDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public ChannelToSessionDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<Guid> GetOrCreateSessionAsync(string userId, string channelInstanceId)
        {
            // TODO: Logik implementieren:
            // 1. Suche in ChatSessionParticipant nach UserId
            // 2. Suche in ChatSession nach ChannelId == channelInstanceId
            // 3. Falls gefunden, SessionId zurückgeben
            // 4. Falls nicht, neue ChatSession erstellen und in DB speichern
            return Guid.NewGuid(); // Dummy für den Aufbau
        }
    }
}
