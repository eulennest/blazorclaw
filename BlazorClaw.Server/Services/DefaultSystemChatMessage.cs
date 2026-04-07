
using BlazorClaw.Core.DTOs;
using System.Text;

namespace BlazorClaw.Server.Services
{
    internal class DefaultSystemChatMessage : ChatMessage
    {
        override public string Role => "system";
        override public object Content
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine("### MEDIA PROTOCOLS ###");
                sb.AppendLine("[IMAGE:URL] [VIDEO:URL] [VOICE:URL] [MUSIC:URL] [FILE:URL] [TTS:Text]");
                sb.AppendLine("Rules: PREPEND tag (no text before). ONE tag max. Escape ']' as '&#93;'.");
                sb.AppendLine();
                sb.AppendLine("### MEMORY vs. FS ###");
                sb.AppendLine("[memory:*.md] → memory_* tools. [other] → fs_* tools.");
                sb.AppendLine("memory_* = virtual FS (relative paths). fs_* = physical FS (sandboxed).");
                return sb.ToString();
            }
        }
    }
    internal class DefaultAssistChatMessage : ChatMessage
    {
        override public string Role => "assistant";
        override public object Content
        {
            get
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
}