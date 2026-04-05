using System.Text.Json;

namespace BlazorClaw.WhatsApp.Protocol
{
    /// <summary>
    /// WhatsApp Authentication State - persistent storage
    /// Stores keys, tokens, and credentials between sessions
    /// </summary>
    public class WhatsAppAuthState
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientToken { get; set; } = string.Empty;
        public string ServerToken { get; set; } = string.Empty;

        // Noise Protocol keys (set during handshake)
        public byte[]? ClientPublicKey { get; set; }
        public byte[]? ClientPrivateKey { get; set; }
        public byte[]? SendKey { get; set; }
        public byte[]? ReceiveKey { get; set; }

        // Signal Protocol identity keys
        public byte[]? IdentityPrivateKey { get; set; }
        public byte[]? IdentityPublicKey { get; set; }

        /// <summary>
        /// Load auth state from disk
        /// </summary>
        public static async Task<WhatsAppAuthState> LoadAsync(string directory)
        {
            var credsFile = Path.Combine(directory, "creds.json");

            if (File.Exists(credsFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(credsFile);
                    return JsonSerializer.Deserialize<WhatsAppAuthState>(json) ?? new();
                }
                catch
                {
                    // If parsing fails, create new state
                    return new WhatsAppAuthState { ClientId = Guid.NewGuid().ToString() };
                }
            }

            return new WhatsAppAuthState { ClientId = Guid.NewGuid().ToString() };
        }

        /// <summary>
        /// Save auth state to disk (encrypted in production!)
        /// </summary>
        public async Task SaveAsync(string directory)
        {
            Directory.CreateDirectory(directory);
            var credsFile = Path.Combine(directory, "creds.json");
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(credsFile, json);
        }
    }
}
