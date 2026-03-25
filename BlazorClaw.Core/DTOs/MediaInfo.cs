using System.Text.Json.Serialization;

namespace BlazorClaw.Core.DTOs
{
    public class MediaInfo
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }
}