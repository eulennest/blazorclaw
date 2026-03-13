using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.DTOs;
using Microsoft.AspNetCore.Identity;
using System.Runtime.InteropServices;
using System.Text;

namespace BlazorClaw.Server.Services
{
    internal class DefaultSystemChatMessage : ChatMessage    {
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
                return sb.ToString();
            }
        }
    }
}