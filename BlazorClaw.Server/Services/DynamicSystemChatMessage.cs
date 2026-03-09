using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Identity;
using System.Text;

namespace BlazorClaw.Server.Services
{
    internal class DynamicSystemChatMessage(ChatSessionState state) : ChatMessage
    {
        public ApplicationUser? User { get; private set; }
        override public string Role => "system";
        override public object Content
        {
            get
            {
                var context = state.Services.GetRequiredService<MessageContextAccessor>().Context!;
                if (User == null && !string.IsNullOrWhiteSpace(context.Channel?.SenderId))
                {
                    User = state.Services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(context.Channel.SenderId).GetAwaiter().GetResult();
                }
                var uinfo = User;

                var tokenProz = (state.LastUsage?.PromptTokens ?? 1) / 100000.0 * 100.0;
                var sb = new StringBuilder();
                sb.AppendLine($"Current Time: {DateTime.UtcNow:R}");
                sb.AppendLine($"Session ID: {context.Session?.Id}");
                sb.AppendLine($"User ID: {context.UserId}");
                sb.AppendLine($"Channel Provider: {context.Channel?.ChannelProvider}");
                sb.AppendLine($"Channel ID: {context.Channel?.ChannelId}");
                sb.AppendLine($"Channel Sender ID: {context.Channel?.SenderId}");
                if (uinfo != null)
                {
                    sb.AppendLine("User-Acount:");
                    sb.AppendLine($" - Name: {uinfo.FirstName} {uinfo.LastName}");
                    sb.AppendLine($" - Email: {uinfo.Email}");
                }
                sb.AppendLine();
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