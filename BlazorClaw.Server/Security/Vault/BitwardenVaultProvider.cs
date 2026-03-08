using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bitwarden.Sdk; // Assuming current SDK structure
using BlazorClaw.Core.Security.Vault;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Server.Security.Vault;

public class BitwardenOptions
{
    public const string Section = "Tools:Bitwarden";
    public string AccessToken { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
}

public class BitwardenVaultProvider : IVaultProvider
{
    private readonly BitwardenClient _client;
    private readonly BitwardenOptions _options;

    public BitwardenVaultProvider(IOptions<BitwardenOptions> options)
    {
        _options = options.Value;
        _client = new BitwardenClient();
        _client.AccessTokenLogin(_options.AccessToken);
    }

    public async Task<IVaultEntry?> GetSecretAsync(string key)
    {
        var uid = Guid.Parse(key);
        var secret = await Task.Run(() => _client.Secrets.Get(uid));
        return new VaultEntry()
        {
            Key = secret.Id.ToString(),
            Secret = secret.Value,
            Title = secret.Key,
            Notes = secret.Note
        };
    }

    public async IAsyncEnumerable<IVaultKey> GetKeysAsync()
    {
        var orgaId = Guid.Parse(_options.OrganizationId);
        var data = await Task.Run(() => _client.Secrets.List(orgaId));
        foreach (var item in data.Data)
        {
            yield return new VaultKey()
            {
                Key = item.Id.ToString(),
                Title = item.Key
            };
        }
    }

    public async Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null)
    {
        var orgaId = Guid.Parse(_options.OrganizationId);

        if (string.IsNullOrWhiteSpace(key))
        {
             return await Task.Run(() =>
                _client.Secrets.Create(title, secret, note ?? string.Empty, orgaId, []).Id.ToString());
        }
        else
        {
            var uid = Guid.Parse(key);
            return await Task.Run(() => _client.Secrets.Update(uid, title, secret, note ?? string.Empty, orgaId, []).Id.ToString());
        }
    }
}
