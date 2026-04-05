namespace BlazorClaw.WhatsApp.Events
{
    public class MessageEvent
    {
        public string MessageId { get; set; } = string.Empty;
        public string RemoteJid { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    public class PresenceEvent
    {
        public string Jid { get; set; } = string.Empty;
        public string Status { get; set; } = "unavailable";
        public long Timestamp { get; set; }
    }

    public class ConnectionEvent
    {
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? QRCode { get; set; }
    }
}
