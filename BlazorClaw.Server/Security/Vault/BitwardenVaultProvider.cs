using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Bitwarden.Sdk;
using BlazorClaw.Core.Security.Vault;
using Microsoft.Extensions.Options;

namespace BlazorClaw.Server.Security.Vault;

public class BitwardenOptions : BitwardenSettings
{
    public const string Section = "Vault:Bitwarden";
    public string AccessToken { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
}

public class BitwardenVaultProvider(
    IOptions<BitwardenOptions> options,
    IEnumerable<VaultProviderInfo> providers,
    ILogger<BitwardenVaultProvider> logger) : IVaultProvider
{
    private readonly BitwardenOptions _options = options.Value;
    private readonly List<VaultProviderInfo> _providers = providers.ToList();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private BitwardenClient? _client;
    private BitwardenOptions? _resolvedOptions;

    public async Task<IVaultEntry?> GetSecretAsync(string key)
    {
        var client = await GetClientAsync();
        var uid = Guid.Parse(key);
        var secret = await Task.Run(() => client.Secrets.Get(uid));
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
        var client = await GetClientAsync();
        var orgaId = GetRequiredOrganizationId();
        var data = await Task.Run(() => client.Secrets.List(orgaId));
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
        var client = await GetClientAsync();
        var orgaId = GetRequiredOrganizationId();

        if (string.IsNullOrWhiteSpace(key))
        {
            return await Task.Run(() =>
               client.Secrets.Create(title, secret, note ?? string.Empty, orgaId, []).Id.ToString());
        }
        else
        {
            var uid = Guid.Parse(key);
            return await Task.Run(() => client.Secrets.Update(uid, title, secret, note ?? string.Empty, orgaId, []).Id.ToString());
        }
    }

    public async Task RemoveSecretAsync(string key)
    {
        var client = await GetClientAsync();
        var uid = Guid.Parse(key);
        await Task.Run(() => client.Secrets.Delete(new[] { uid }));
    }

    private async Task<BitwardenClient> GetClientAsync()
    {
        if (_client != null) return _client;

        await _initLock.WaitAsync();
        try
        {
            if (_client != null) return _client;

            _resolvedOptions = await ResolveOptionsAsync();
            _client = new BitwardenClient(_resolvedOptions);
            _client.AccessTokenLogin(_resolvedOptions.AccessToken);
            logger.LogInformation("Bitwarden provider initialized against {ApiUrl}", _resolvedOptions.ApiUrl);
            return _client;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<BitwardenOptions> ResolveOptionsAsync()
    {
        if (HasDirectConfig(_options))
            return _options;

        foreach (var provider in _providers.Where(o => o.Id.StartsWith("db-", StringComparison.InvariantCultureIgnoreCase)))
        {
            await foreach (var key in provider.Provider.GetKeysAsync())
            {
                if (!key.Title.Contains("vaultwarden", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                var entry = await provider.Provider.GetSecretAsync(key.Key);
                if (entry == null || string.IsNullOrWhiteSpace(entry.Secret))
                    continue;

                var url = TryParseUrl(entry.Notes);
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var resolved = new BitwardenOptions
                {
                    ApiUrl = url,
                    IdentityUrl = url,
                    AccessToken = entry.Secret,
                    OrganizationId = TryParseOrganizationId(entry.Notes) ?? _options.OrganizationId
                };

                logger.LogInformation("Bitwarden provider bootstrapped from {ProviderId} using key {Title}", provider.Id, entry.Title);
                return resolved;
            }
        }

        throw new InvalidOperationException("Bitwarden/Vaultwarden Konfiguration fehlt. Weder appsettings noch ein db-* Key mit 'vaultwarden' im Titel und URL in den Notes gefunden.");
    }

    private Guid GetRequiredOrganizationId()
    {
        if (string.IsNullOrWhiteSpace(_resolvedOptions?.OrganizationId))
            throw new InvalidOperationException("Bitwarden OrganizationId fehlt. Bitte in der Konfiguration oder in den Vault-Notes hinterlegen.");
        return Guid.Parse(_resolvedOptions.OrganizationId);
    }

    private static bool HasDirectConfig(BitwardenOptions options)
        => !string.IsNullOrWhiteSpace(options.AccessToken)
            && (!string.IsNullOrWhiteSpace(options.ApiUrl) || !string.IsNullOrWhiteSpace(options.IdentityUrl));

    private static string? TryParseUrl(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var match = Regex.Match(notes, @"https?://[^\s]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Value.TrimEnd('/', ';', ',', '.') : null;
    }

    private static string? TryParseOrganizationId(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        foreach (var line in notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if ((key.Equals("org", StringComparison.InvariantCultureIgnoreCase)
                    || key.Equals("organization", StringComparison.InvariantCultureIgnoreCase)
                    || key.Equals("organizationid", StringComparison.InvariantCultureIgnoreCase)
                    || key.Equals("organization-id", StringComparison.InvariantCultureIgnoreCase))
                && Guid.TryParse(value, out var orgId))
            {
                return orgId.ToString();
            }
        }

        var guidMatch = Regex.Match(notes, @"\b[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return guidMatch.Success ? Guid.Parse(guidMatch.Value).ToString() : null;
    }
}
