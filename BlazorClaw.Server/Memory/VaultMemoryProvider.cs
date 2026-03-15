using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Security.Vault;

namespace BlazorClaw.Server.Memory
{
    public class VaultMemoryProvider(IVaultProvider vaultProvider) : IMemorySearchProvider
    {
        public async IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults, MessageContext? context)
        {
            var results = new List<string>();

            await foreach (var key in vaultProvider.GetKeysAsync())
            {
                foreach (var query in queries)
                {
                    if (key.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || key.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return $"(Vault-Eintrag gefunden) Schlüssel: '{key.Key}'. " +
                                     $"Du kannst diesen Eintrag mit dem 'vault_get' Tool abrufen, " +
                                     $"indem du '{key.Key}' als 'Key'-Parameter verwendest.";
                        break;
                    }
                }
                if (results.Count >= maxResults) break;
            }
        }
    }
}
