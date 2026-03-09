using BlazorClaw.Core.DTOs;
using System.Text;

namespace BlazorClaw.Server.Services
{
    internal class DynamicSystemChatMessage(IServiceProvider serviceProvider) : ChatMessage
    {
        override public string Role => "system";
        override public object Content
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Current Time: {DateTime.Now:R}");
                return sb.ToString();
            }
        }
    }
}