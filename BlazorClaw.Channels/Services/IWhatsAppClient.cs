namespace BlazorClaw.Channels.Services
{
    /// <summary>
    /// WhatsApp client abstraction - decouples from Baileys.NET implementation
    /// </summary>
    public interface IWhatsAppClient : IChannelBot
    {
        /// <summary>
        /// Account identifier (e.g., "default", "business")
        /// </summary>
        string AccountId { get; }

        /// <summary>
        /// Connect to WhatsApp servers and authenticate via QR code
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnect from WhatsApp servers
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a message to a JID (user or group)
        /// </summary>
        Task SendMessage(string jid, object messageContent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send read receipt for a message
        /// </summary>
        Task SendReadReceipt(string jid, string messageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fired when a message is received
        /// </summary>
        event EventHandler<(string jid, string message)>? OnMessageReceived;

        /// <summary>
        /// Fired when presence changes (online/offline/typing)
        /// </summary>
        event EventHandler<(string jid, string presence)>? OnPresenceUpdate;

        /// <summary>
        /// Fired when connection state changes
        /// </summary>
        event EventHandler<string>? OnConnectionUpdate;

        /// <summary>
        /// Fired when QR code is available for scanning
        /// </summary>
        event EventHandler<string>? OnQRCode;
    }

    /// <summary>
    /// WhatsApp account configuration
    /// </summary>
    public class WhatsAppAccountConfig
    {
        /// <summary>
        /// Enable/disable this account
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Phone number for authentication (optional display)
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Directory for storing auth state (credentials, keys, etc.)
        /// </summary>
        public string AuthDir { get; set; } = "./auth/whatsapp_default";

        /// <summary>
        /// DM Policy: "pairing", "allowlist", "open"
        /// </summary>
        public string DmPolicy { get; set; } = "pairing";

        /// <summary>
        /// Whitelist of allowed senders (phone numbers or Jids)
        /// </summary>
        public List<string>? AllowFrom { get; set; }

        /// <summary>
        /// Group handling config
        /// </summary>
        public WhatsAppGroupConfig? Groups { get; set; }
    }

    /// <summary>
    /// Group-specific configuration
    /// </summary>
    public class WhatsAppGroupConfig
    {
        /// <summary>
        /// Group policy: "pairing", "allowlist", "open"
        /// </summary>
        public string Policy { get; set; } = "allowlist";

        /// <summary>
        /// Allowed group Jids (e.g., "123-456@g.us")
        /// </summary>
        public Dictionary<string, GroupMetadata>? Allowed { get; set; }

        /// <summary>
        /// Whether mention is required to trigger bot
        /// </summary>
        public bool RequireMention { get; set; } = true;
    }

    /// <summary>
    /// Group metadata
    /// </summary>
    public class GroupMetadata
    {
        public string Jid { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public bool RequireMention { get; set; } = true;
    }
}
