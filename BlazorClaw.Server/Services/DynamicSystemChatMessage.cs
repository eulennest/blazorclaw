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
                var maxtoken = 100000;

                var tokenProz = (state.LastUsage?.PromptTokens ?? 1) / maxtoken * 100.0;
                var sb = new StringBuilder();
                sb.AppendLine($"Current Time: {DateTime.UtcNow:R}");
                sb.AppendLine($"Current Model: {context.Session?.CurrentModel}");
                sb.AppendLine($"Current OS: {RuntimeInformation.OSDescription}");
                sb.AppendLine($"Session Title: {context.Session?.Title}");
                sb.AppendLine($"Session ID: {context.Session?.Id}");
                sb.AppendLine($"Channel Provider: {context.Channel?.ChannelProvider}");
                sb.AppendLine($"Channel ID: {context.Channel?.ChannelId}");
                sb.AppendLine($"Channel Sender ID: {context.Channel?.SenderId}");
                sb.AppendLine($"User ID: {context.UserId}");
                if (uinfo != null)
                {
                    sb.AppendLine("User-Account:");
                    sb.AppendLine($" - Name: {uinfo.FirstName} {uinfo.LastName}");
                    sb.AppendLine($" - Email: {uinfo.Email}");
                }
                sb.AppendLine();
                sb.AppendLine($"Token usage: {state.LastUsage?.PromptTokens} / {maxtoken / 1000}k ({tokenProz} %)");
                if (tokenProz > 80)
                {
                    sb.AppendLine("!!! WARNUNG: TOKEN LIMIT FAST ERREICHT !!!");
                    sb.AppendLine("Warning: Token usage is above 80% of the limit. Use the session_compress Tool for compression!");
                    sb.AppendLine("Du MUSST deine Antworten extrem kurz halten. KEINE ausführlichen Erklärungen mehr. Fasse dich auf das Wesentliche!");
                }
                sb.AppendLine();

                // --- Kanal-spezifischer Kontext ---
                // Annahme: context.ChannelProvider liefert dir "WebChat", "Telegram", "Matrix" etc.
                string channel = context.Channel?.ChannelProvider.ToLower() ?? "generic";
                sb.AppendLine($"Channel: {channel}");

                // --- Dynamische Instruktion für die KI ---
                sb.AppendLine("--- Formatierungs-Instruktionen ---");
                if (channel == "webchat")
                {
                    sb.AppendLine("Du befindest dich im WebChat. Du darfst Tabellen, Markdown-Blöcke und ausführliche Formatierungen nutzen.");
                    sb.AppendLine("Du kannst Bilder via Markdown-Image-Tag rendern.");
                }
                else if (channel == "telegram")
                {
                    sb.AppendLine("Du befindest dich in Telegram. Halte Antworten kurz, nutze einfaches Markdown (fett/kursiv) und keine komplexen Tabellen.");
                }
                else if (channel == "matrix")
                {
                    sb.AppendLine("Du befindest dich in Matrix. Nutze leichtes Markdown, vermeide aber breite Tabellen.");
                }
                else
                {
                    sb.AppendLine("Nutze leichtes Markdown, vermeide aber breite Tabellen.");
                }

                sb.AppendLine("Hinweis: Passe deine Antwort immer an die Möglichkeiten und Beschränkungen des jeweiligen Kanals an.");
                return sb.ToString();
            }
        }
    }
}