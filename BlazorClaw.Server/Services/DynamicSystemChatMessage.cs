using BlazorClaw.Core.Commands;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using System.Resources;
using System.Text;

namespace BlazorClaw.Server.Services
{
    internal class DynamicSystemChatMessage(IServiceProvider serviceProvider, MessageContext context) : ChatMessage
    {
        private ChatSessionState? state;
        override public string Role => "system";
        override public object Content
        {
            get
            {
                if (state == null)
                {
                    var sm = serviceProvider.GetRequiredService<ISessionManager>();
                    state = sm.GetOrCreateSessionAsync(context.Session.Id).GetAwaiter().GetResult();
                }
                var tokenProz = (state.LastUsage?.PromptTokens ?? 1) / 100000.0 * 100.0;
                var sb = new StringBuilder();
                sb.AppendLine($"Current Time: {DateTime.Now:R}");
                sb.AppendLine($"Token usage: {state.LastUsage?.PromptTokens} / 100k ({tokenProz} %)");
                if (tokenProz > 80)
                {
                    sb.AppendLine("Warning: Token usage is above 80% of the limit. Use the session_compress Tool for compression!");
                }
                return sb.ToString();
            }
        }
    }
}