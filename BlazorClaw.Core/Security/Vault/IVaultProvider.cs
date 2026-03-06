namespace BlazorClaw.Core.Security.Vault;

public interface IVaultProvider
{
    string? GetSecret(string key);
}
