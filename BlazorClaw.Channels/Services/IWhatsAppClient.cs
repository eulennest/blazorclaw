using BlazorClaw.Core.Sessions;

namespace BlazorClaw.Channels.Services
{ 
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
