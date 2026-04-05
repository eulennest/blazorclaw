using System.Text.Json;

namespace BlazorClaw.WhatsApp.Protocol
{
    /// <summary>
    /// WhatsApp Authentication State - persistent storage
    /// </summary>
    public class WhatsAppAuthState
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientToken { get; set; } = string.Empty;
        public string ServerToken { get; set; } = string.Empty;
        public Dictionary<string, string> KeyPair { get; set; } = new();
        public byte[]? EncryptionKey { get; set; }

        /// <summary>
        /// Load auth state from disk
        /// </summary>
        public static async Task<WhatsAppAuthState> LoadAsync(string directory)
        {
            var credsFile = Path.Combine(directory, "creds.json");

            if (File.Exists(credsFile))
            {
                var json = await File.ReadAllTextAsync(credsFile);
                return JsonSerializer.Deserialize<WhatsAppAuthState>(json) ?? new();
            }

            return new WhatsAppAuthState
            {
                ClientId = Guid.NewGuid().ToString(),
            };
        }

        /// <summary>
        /// Save auth state to disk
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
