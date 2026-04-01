
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
}