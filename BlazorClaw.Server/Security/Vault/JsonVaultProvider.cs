using System.Text.Json;

namespace BlazorClaw.Core.Security.Vault;

public class JsonVaultProvider : IVaultProvider
{
    private readonly string _filePath;
    private Dictionary<string, string>? _secrets;

    public JsonVaultProvider(string filePath)
    {
        _filePath = filePath;
    }

    public string? GetSecret(string key)
    {
        if (_secrets == null)
        {
            if (!File.Exists(_filePath)) return null;
            var json = File.ReadAllText(_filePath);
            _secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        return _secrets?.GetValueOrDefault(key);
    }
}
