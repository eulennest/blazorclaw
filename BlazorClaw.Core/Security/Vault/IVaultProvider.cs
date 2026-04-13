namespace BlazorClaw.Core.Security.Vault;

public interface IVaultProvider
{
    IAsyncEnumerable<IVaultKey> GetKeysAsync();
    Task<IVaultEntry?> GetSecretAsync(string key);
    Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null);
    Task RemoveSecretAsync(string key);
}

public interface IVaultKey
{
    string Key { get; }
    string Title { get; }
}
public interface IVaultEntry : IVaultKey
{
    string Secret { get; }
    string Notes { get; }
}

public interface IProviderVaultKey : IVaultKey
{
    string Provider { get; }
}

public interface IProviderVaultEntry : IVaultEntry, IProviderVaultKey
{
}

public interface IVaultManager
{
    IEnumerable<IVaultProviderInfo> GetProviders();
    IVaultProviderInfo? GetProvider(string provider);
    IAsyncEnumerable<IProviderVaultKey> GetKeysAsync(string? provider = null);
    Task<IProviderVaultEntry?> GetSecretAsync(string key, string? provider = null);
    Task<string> SetSecretAsync(string provider, string title, string secret, string? note = null, string? key = null);
    Task RemoveSecretAsync(string provider, string key);
}

public interface IVaultProviderInfo
{
    string Id { get; }
    string Type { get; }
    string Title { get; }
    string? Description { get; }
    bool CanWrite { get; }
}

public class VaultProviderInfo : IVaultProviderInfo
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool CanWrite { get; init; }
    public required IVaultProvider Provider { get; init; }

    public override string ToString() => $"{Title} [{Id}]";
}

public class ProviderVaultKey(string provider, IVaultKey inner) : IProviderVaultKey
{
    protected IVaultKey Inner { get; } = inner;
    public string Provider { get; } = provider;
    public string Key => Inner.Key;
    public string Title => Inner.Title;

    public override string ToString() => $"{Title} ({Key}) @ {Provider}";
}

public class ProviderVaultEntry(string provider, IVaultEntry inner) : ProviderVaultKey(provider, inner), IProviderVaultEntry
{
    protected IVaultEntry Entry { get; } = inner;
    public string Secret => Entry.Secret;
    public string Notes => Entry.Notes;

    public override string ToString() => $"{Title} ({Key}) @ {Provider}";
}
