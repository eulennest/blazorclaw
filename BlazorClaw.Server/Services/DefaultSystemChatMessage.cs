
using BlazorClaw.Core.DTOs;
using Microsoft.Extensions.AI;
using System.Text;

namespace BlazorClaw.Server.Services
{
    internal class DefaultSystemChatMessage : ChatMessage
    {
        public DefaultSystemChatMessage() : base(ChatRole.System, BuildText())
        {
            CreatedAt = DateTimeOffset.UtcNow;
        }

        private static string BuildText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("### MEDIA PROTOCOLS ###");
            sb.AppendLine("You can use special tags to output media:");
            sb.AppendLine();
            sb.AppendLine("Available tags:");
            sb.AppendLine("- [TTS:Text] = Text-to-Speech output (prepend at start of message)");
            sb.AppendLine("- [IMAGE:URL] = Display image");
            sb.AppendLine("- [VIDEO:URL] = Display video");
            sb.AppendLine("- [VOICE:URL] = Play voice file");
            sb.AppendLine("- [MUSIC:URL] = Play music");
            sb.AppendLine("- [FILE:URL] = Link to file");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- PREPEND tag at message start (no text before)");
            sb.AppendLine("- ONE tag maximum per message");
            sb.AppendLine("- Escape ']' character as '&#93;' inside tag content");
            sb.AppendLine("- Text can be added after the tag");
            sb.AppendLine();
            sb.AppendLine("Examples:");
            sb.AppendLine("- `[TTS:Hallo Daniel, wie geht es dir?]` Make TTS as Voice Bubble");
            sb.AppendLine("- `[IMAGE:https:/example.org/eule.jpg] Bild einer Eule` Send a Image with text");
            sb.AppendLine();
            sb.AppendLine("### INCOMING VOICE MESSAGES ###");
            sb.AppendLine("User voice messages arrive as:");
            sb.AppendLine("`[VOICE MSG:URL] Transcription:\\n{transcribed text}`");
            sb.AppendLine();
            sb.AppendLine("- Read the transcription (main content)");
            sb.AppendLine("- URL available if needed for context");
            sb.AppendLine();
            sb.AppendLine("### MEMORY vs. FS ###");
            sb.AppendLine("[memory:*.md] → memory_* tools. [other] → fs_* tools.");
            sb.AppendLine("memory_* = virtual FS (relative paths). fs_* = virtual FS (sandboxed, mapped to phyiscal FS).");
            sb.AppendLine();
            sb.AppendLine("### NO_REPLY SIGNAL ###");
            sb.AppendLine("NO_REPLY = suppress user output (for system triggers only)");
            sb.AppendLine("Usage: Reply ONLY with `NO_REPLY`, nothing before/after.");
            sb.AppendLine("Context: CRON/Heartbeat/System triggers where no user message needed.");
            return sb.ToString();
        }
    }

    internal class DefaultAssistChatMessage : ChatMessage
    {
        public DefaultAssistChatMessage() : base(ChatRole.Assistant, BuildText())
        {
            CreatedAt = DateTimeOffset.UtcNow;
        }

        private static string BuildText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WICHTIGE ERKENNTNISSE ===");
            sb.AppendLine();
            sb.AppendLine("• TOKEN WARNUNG (>80%): SOFORT session_compress() aufrufen - NICHT fragen!");
            sb.AppendLine("• SYSTEMPROMPT.md: Wird bei JEDER Nachricht frisch geladen - Änderungen wirken SOFORT!");
            sb.AppendLine("• Dynamic System Info: Token, Zeit, Model sind LIVE - bei jeder Nachricht aktualisiert");
            sb.AppendLine();
            sb.AppendLine("Channel-Regeln:");
            sb.AppendLine("• webchat: Tabellen & Markdown erlaubt");
            sb.AppendLine("• telegram/matrix: Kurz halten, keine breiten Tabellen");
            sb.AppendLine();
            sb.AppendLine("Systemprompts: Alle role=system Nachrichten oben sichtbar mit [memory: Datei.md] Header");
            sb.AppendLine();
            sb.AppendLine("=== ERKENNTNISSE ENDE ===");
            return sb.ToString();
        }
    }
}