namespace BlazorClaw.Core.Security.Vault;

public interface IVaultProvider
{
    IAsyncEnumerable<string> GetKeysAsync();
    Task<string> GetSecretAsync(string key);
    Task SetSecretAsync(string key, string secret);
}
