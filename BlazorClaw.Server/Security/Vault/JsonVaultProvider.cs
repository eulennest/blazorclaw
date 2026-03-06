using BlazorClaw.Core.Security.Vault;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace BlazorClaw.Server.Security.Vault;

public class JsonVaultProvider(IOptions<JsonVaultOptions> options) : IVaultProvider
{
    private Dictionary<string, object>? _secrets;

    private string GetFilePath()
    {
        return options.Value.FilePath;
    }
    public async IAsyncEnumerable<string> GetKeysAsync()
    {
        if (_secrets == null)
        {
            var filePath = GetFilePath();
            if (!File.Exists(filePath)) yield break;
            var json = await File.ReadAllTextAsync(filePath);
            _secrets = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        if (_secrets != null)
            foreach (var item in _secrets.Keys)
            {
                yield return item;
            }
    }

    public async Task SetSecretAsync(string key, string value)
    {
        var filePath = GetFilePath();
        if (File.Exists(filePath))
        {
            var json = await File.ReadAllTextAsync(filePath);
            _secrets = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? [];
        }
        else
        {
            _secrets = [];
        }

        _secrets[key] = value;
        var updatedJson = JsonSerializer.Serialize(_secrets);
        await File.WriteAllTextAsync(filePath, updatedJson);
    }


    public async Task<string> GetSecretAsync(string key)
    {
        if (_secrets == null)
        {
            var filePath = GetFilePath();
            if (!File.Exists(filePath)) throw new KeyNotFoundException();
            var json = await File.ReadAllTextAsync(filePath);
            _secrets = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        if (_secrets != null && _secrets.TryGetValue(key, out var val))
            return Convert.ToString(val) ?? string.Empty;
        throw new KeyNotFoundException();
    }
}
