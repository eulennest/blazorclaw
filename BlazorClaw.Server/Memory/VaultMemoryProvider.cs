using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Security.Vault;

namespace BlazorClaw.Server.Memory
{
    public class VaultMemoryProvider : IMemorySearchProvider
    {
        private readonly IVaultProvider _vaultProvider;

        public VaultMemoryProvider(IVaultProvider vaultProvider)
        {
            _vaultProvider = vaultProvider;
        }

        public async Task<string> SearchAsync(string[] queries, int maxResults)
        {
            var results = new List<string>();

            foreach (var query in queries)
            {
                await foreach (var key in _vaultProvider.GetKeysAsync())
                {
                    if (key.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add($"(Vault-Eintrag gefunden) Schlüssel: '{key}'. " +
                                     $"Du kannst diesen Eintrag mit dem 'vault_get' Tool abrufen, " +
                                     $"indem du '{key}' als 'Key'-Parameter verwendest.");
                        
                        if (results.Count >= maxResults) break;
                    }
                }
            }

            return string.Join("\n", results);
        }
    }
}
