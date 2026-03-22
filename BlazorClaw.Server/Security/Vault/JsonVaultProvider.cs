using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Security.Vault;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BlazorClaw.Server.Security.Vault;

public class JsonVaultProvider(IOptions<JsonVaultOptions> options, MessageContextAccessor mca) : IVaultProvider
{
    private Dictionary<string, VaultEntry>? _secrets;

    private string GetFilePath()
    {
        return Path.Combine(Core.Utils.PathUtils.GetAllBasePath(mca.Context), "secure", options.Value.FilePath);
    }
    public async IAsyncEnumerable<IVaultKey> GetKeysAsync()
    {
        if (_secrets == null)
        {
            var filePath = GetFilePath();
            if (!File.Exists(filePath)) yield break;
            var json = await File.ReadAllTextAsync(filePath);
            _secrets = JsonSerializer.Deserialize<Dictionary<string, VaultEntry>>(json);
        }
        if (_secrets != null)
            foreach (var item in _secrets)
            {
                yield return new VaultKey() { Key = item.Key, Title = item.Value.Title };
            }
    }


    public async Task<IVaultEntry?> GetSecretAsync(string key)
    {
        if (_secrets == null)
        {
            var filePath = GetFilePath();
            if (!File.Exists(filePath)) throw new KeyNotFoundException();
            var json = await File.ReadAllTextAsync(filePath);
            _secrets = JsonSerializer.Deserialize<Dictionary<string, VaultEntry>>(json);
        }
        if (_secrets != null && _secrets.TryGetValue(key, out var val))
            return val;
        throw new KeyNotFoundException();
    }

    public async Task<string> SetSecretAsync(string title, string secret, string? note = null, string? key = null)
    {
        var filePath = GetFilePath();
        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath);
            _secrets = JsonSerializer.Deserialize<Dictionary<string, VaultEntry>>(json) ?? [];
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        }
        _secrets ??= [];
        if (string.IsNullOrWhiteSpace(key))
            key = Guid.NewGuid().ToString();
        _secrets.TryGetValue(key, out var existing);
        existing ??= new VaultEntry() { Key = key };
        existing.Title = title;
        existing.Secret = secret;
        existing.Notes = note ?? existing.Notes;
        _secrets[key] = existing;
        var updatedJson = JsonSerializer.Serialize(_secrets);
        await File.WriteAllTextAsync(filePath, updatedJson);
        return key;
    }
}
