using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Security.Vault;

namespace BlazorClaw.Server.Memory
{
    public class VaultMemoryProvider() : IMemorySearchProvider
    {
        public async IAsyncEnumerable<string> SearchAsync(string[] queries, int maxResults, MessageContext? context)
        {
            if (context == null) yield break;
            var vaultProvider = context.Provider.GetRequiredService<IVaultProvider>();
            var results = 0;

            await foreach (var key in vaultProvider.GetKeysAsync())
            {
                foreach (var query in queries)
                {
                    if (key.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || key.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results++;
                        yield return $"[Vault: {key.Key}]\nTitle:{key.Title}\nuse vault_get('{key.Key}')"; break;
                    }
                }
                if (results >= maxResults) break;
            }
        }
    }
}
