using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Identity;
using System.Runtime.InteropServices;
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
                if (User == null && !string.IsNullOrWhiteSpace(context.UserId))
                {
                    User = state.Services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(context.UserId).GetAwaiter().GetResult();
                }
                var uinfo = User;
                const int maxtoken = 100000;
                const int warningThreshold = 80;

                var tokenProz = (state.LastUsage?.PromptTokens ?? 1) / maxtoken * 100.0;
                var sb = new StringBuilder();

                // Kompakte Metadaten (Tab-Format statt AppendLine)
                sb.AppendLine($"Time: {DateTime.UtcNow:u}");
                sb.AppendLine($"Model: {context.Session?.CurrentModel} | OS: {RuntimeInformation.OSDescription}");
                sb.AppendLine($"Session: {context.Session?.Title} ({context.Session?.Id})");
                sb.AppendLine($"Channel: {context.Channel?.ChannelProvider} | User: {context.UserId}");

                if (uinfo != null)
                    sb.AppendLine($"Account: {uinfo.FirstName} {uinfo.LastName} <{uinfo.Email}>");

                sb.AppendLine($"Tokens: {state.LastUsage?.PromptTokens}/{maxtoken / 1000}k ({tokenProz:F1}%)");

                if (tokenProz > warningThreshold)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠️ TOKEN LIMIT CRITICAL (>80%)");
                    sb.AppendLine("Use session_compress IMMEDIATELY. Keep answers SHORT.");
                }

                sb.AppendLine();

                // Channel-spezifische Instruktionen (gekürzt)
                string channel = context.Channel?.ChannelProvider.ToLower() ?? "generic";
                sb.AppendLine($"Channel: {channel}");

                switch (channel)
                {
                    case "webchat":
                        sb.AppendLine("Format: Tabellen, Markdown, Bilder erlaubt.");
                        break;
                    case "telegram":
                        sb.AppendLine("Format: Kurz halten, einfaches Markdown (fett/kursiv), keine Tabellen.");
                        break;
                    case "matrix":
                        sb.AppendLine("Format: Leichtes Markdown, keine breiten Tabellen.");
                        break;
                    default:
                        sb.AppendLine("Format: Leichtes Markdown, keine breiten Tabellen.");
                        break;
                }

                return sb.ToString();
            }
        }
    }
}