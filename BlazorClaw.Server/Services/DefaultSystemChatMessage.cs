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
                sb.AppendLine("- Use these tags at the ABSOLUTE START of your response if sending media:");
                sb.AppendLine("  [IMAGE:URL]      - Send/Display a still image.");
                sb.AppendLine("  [VIDEO:URL]      - Send/Play a video file.");
                sb.AppendLine("  [VOICE:URL]      - Send/Play an audio/voice clip.");
                sb.AppendLine("  [MUSIC:URL]      - Send/Play a music file.");
                sb.AppendLine("  [FILE:URL]       - Send/Provide a downloadable file link.");
                sb.AppendLine("  [TTS:Text]       - Synthesize audio from the provided text.");
                sb.AppendLine();
                sb.AppendLine("- Rule: PREPEND tag to the response. NO text or whitespace before the tag.");
                sb.AppendLine("- Rule: Only ONE media tag permitted per response.");
                sb.AppendLine("- Rule: If your content (e.g. TTS text/filename) contains ']', YOU MUST ESCAPE IT as '&#93;'.");
                sb.AppendLine("- Rule: Optional Text message must follow the media tag.");
                sb.AppendLine();
                sb.AppendLine("### MEMORY vs. FS ###");
                sb.AppendLine(" - If a File Marked with [memory: FILENAME.md] OR [XXX memory: FILENAME.md] then use memory_* Tools for actions.");
                sb.AppendLine(" - Memory has only *.md Files, if you read/edit a *.md File you should use memory_* Tools");
                sb.AppendLine(" - All other files MUST use fs_* Tools");
                sb.AppendLine(" - Memory_* Tools are working in a virtual Fs and use relative Paths.");
                sb.AppendLine(" - fs_* Tools working with pyhsical Files and can be Sandboxed.");

                return sb.ToString();
            }
        }
    }
}