namespace BlazorClaw.Core.Security.Vault;

public interface IVaultProvider
{
    IAsyncEnumerable<IVaultKey> GetKeysAsync();
    Task<IVaultEntry?> GetSecretAsync(string key);
    Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null);
}

public interface IVaultKey
{
    string Key { get; set; }
    string Title { get; set; }
}
public interface IVaultEntry : IVaultKey
{
    string Secret { get; set; }
    string Notes { get; set; }
}


public class VaultAggregator(IEnumerable<IVaultProvider> provider) : IVaultProvider
{
    private readonly List<IVaultProvider> Providers = [.. provider];

    private Dictionary<string, IVaultProvider> keysAll = [];
    public async IAsyncEnumerable<IVaultKey> GetKeysAsync()
    {
        Dictionary<string, IVaultProvider> keys = [];
        foreach (var prov in Providers)
        {
            await foreach (var item in prov.GetKeysAsync())
            {
                keys.TryAdd(item.Key, prov);
                yield return item;
            }
        }
        keysAll = keys;

    }

    public Task<IVaultEntry?> GetSecretAsync(string key)
    {
        if (keysAll.TryGetValue(key, out var prov))
            return prov.GetSecretAsync(key);
        return Task.FromResult<IVaultEntry?>(null);
    }

    public Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null)
    {
        if (!string.IsNullOrWhiteSpace(key) && keysAll.TryGetValue(key, out var prov))
            return prov.SetSecretAsync(title, secret, note, key);
        return Providers.First().SetSecretAsync(title, secret, note, key);
    }
}
