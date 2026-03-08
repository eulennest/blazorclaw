namespace BlazorClaw.Core.Security.Vault;

public interface IVaultProvider
{
    IAsyncEnumerable<string> GetKeysAsync();
    Task<string?> GetSecretAsync(string key);
    Task SetSecretAsync(string key, string secret);
}

public class VaultAggregator(IEnumerable<IVaultProvider> provider) : IVaultProvider
{
    private readonly List<IVaultProvider> Providers = [.. provider];

    private Dictionary<string, IVaultProvider> keysAll = [];
    public async IAsyncEnumerable<string> GetKeysAsync()
    {
        Dictionary<string, IVaultProvider> keys = [];
        foreach (var prov in Providers)
        {
            await foreach (var item in prov.GetKeysAsync())
            {
                keys.TryAdd(item, prov);
                yield return item;
            }
        }
        keysAll = keys;

    }

    public Task<string?> GetSecretAsync(string key)
    {
        if (keysAll.TryGetValue(key, out var prov))
            return prov.GetSecretAsync(key);
        return Task.FromResult<string?>(null);
    }

    public Task SetSecretAsync(string key, string secret)
    {
        if (keysAll.TryGetValue(key, out var prov))
            return prov.SetSecretAsync(key, secret);
        return Providers.First().SetSecretAsync(key, secret);
    }
}
