using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.DTOs;
using BlazorClaw.Core.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace BlazorClaw.Server.Services
{
    internal class DynamicSystemChatMessage : ChatMessage
    {
        private readonly ChatSessionState state;

        public DynamicSystemChatMessage(ChatSessionState state) : base(ChatRole.System, new List<AIContent>())
        {
            this.state = state;
            Contents.Add(new TextContent(BuildText()));
            CreatedAt = DateTimeOffset.UtcNow;
        }

        [JsonIgnore]
        public ApplicationUser? User { get; private set; }
        private string BuildText()
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
                sb.AppendLine();
                sb.AppendLine("🔴 CRITICAL: If you see this message:");
                sb.AppendLine("→ IMMEDIATELY call session_compress()");
                sb.AppendLine("→ Do NOT wait, do NOT ask, do NOT think 'I saw this before'");
                sb.AppendLine("→ This prompt is LIVE and DYNAMIC — check it EVERY message!");
                sb.AppendLine("→ Use session_compress IMMEDIATELY. Keep answers SHORT.");
                sb.AppendLine();
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
