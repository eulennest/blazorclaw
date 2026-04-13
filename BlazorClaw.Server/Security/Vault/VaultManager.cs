using BlazorClaw.Core.Security.Vault;

namespace BlazorClaw.Server.Security.Vault;

public class VaultManager(IEnumerable<VaultProviderInfo> providers) : IVaultManager
{
    private readonly Dictionary<string, VaultProviderInfo> _providers = providers
        .ToDictionary(o => o.Id, StringComparer.InvariantCultureIgnoreCase);

    public IEnumerable<IVaultProviderInfo> GetProviders() => _providers.Values.OrderBy(o => o.Id);

    public IVaultProviderInfo? GetProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return null;
        _providers.TryGetValue(provider, out var ret);
        return ret;
    }

    public async IAsyncEnumerable<IProviderVaultKey> GetKeysAsync(string? provider = null)
    {
        if (!string.IsNullOrWhiteSpace(provider))
        {
            var item = RequireProvider(provider);
            await foreach (var key in item.Provider.GetKeysAsync())
                yield return new ProviderVaultKey(item.Id, key);
            yield break;
        }

        foreach (var item in _providers.Values.OrderBy(o => o.Id))
        {
            await foreach (var key in item.Provider.GetKeysAsync())
                yield return new ProviderVaultKey(item.Id, key);
        }
    }

    public async Task<IProviderVaultEntry?> GetSecretAsync(string key, string? provider = null)
    {
        if (!string.IsNullOrWhiteSpace(provider))
        {
            var item = RequireProvider(provider);
            var secret = await GetSecretInternalAsync(item, key);
            return secret == null ? null : new ProviderVaultEntry(item.Id, secret);
        }

        foreach (var item in _providers.Values.OrderBy(o => o.Id))
        {
            var secret = await GetSecretInternalAsync(item, key);
            if (secret != null) return new ProviderVaultEntry(item.Id, secret);
        }

        return null;
    }

    public Task<string> SetSecretAsync(string provider, string title, string secret, string? note = null, string? key = null)
    {
        var item = RequireProvider(provider);
        if (!item.CanWrite)
            throw new InvalidOperationException($"Vault provider '{item.Id}' ist read-only.");
        return item.Provider.SetSecretAsync(title, secret, note, key);
    }

    public Task RemoveSecretAsync(string provider, string key)
    {
        var item = RequireProvider(provider);
        if (!item.CanWrite)
            throw new InvalidOperationException($"Vault provider '{item.Id}' ist read-only.");
        return item.Provider.RemoveSecretAsync(key);
    }

    private VaultProviderInfo RequireProvider(string provider)
    {
        if (!_providers.TryGetValue(provider, out var item))
            throw new KeyNotFoundException($"Vault provider '{provider}' nicht gefunden.");
        return item;
    }

    private static async Task<IVaultEntry?> GetSecretInternalAsync(VaultProviderInfo item, string key)
    {
        var secret = await item.Provider.GetSecretAsync(key);
        if (secret != null) return secret;

        await foreach (var sk in item.Provider.GetKeysAsync())
        {
            if (key.Equals(sk.Title, StringComparison.InvariantCultureIgnoreCase))
                return await item.Provider.GetSecretAsync(sk.Key);
        }

        return null;
    }
}
