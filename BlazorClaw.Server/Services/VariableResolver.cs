using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Services;

namespace BlazorClaw.Server.Services;

/// <summary>
/// Resolves variables from Vault and Environment
/// </summary>
public class VariableResolver : IVariableResolver
{
    private readonly ILogger<VariableResolver> _logger;

    public VariableResolver(ILogger<VariableResolver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Resolve a variable from Vault or Environment
    /// Format: "vault:ItemName" or "env:VAR_NAME"
    /// </summary>
    public async Task<string> ResolveAsync(string source, MessageContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        var parts = source.Split(':', 2);
        if (parts.Length != 2)
        {
            _logger.LogWarning("Invalid variable source format: {Source}", source);
            return string.Empty;
        }

        var sourceType = parts[0].ToLowerInvariant();
        var sourceName = parts[1];

        return sourceType switch
        {
            "vault" => await ResolveFromVaultAsync(sourceName, context),
            "env" => ResolveFromEnvironment(sourceName),
            _ => throw new InvalidOperationException($"Unknown variable source: {sourceType}")
        };
    }

    /// <summary>
    /// Resolve all mappings
    /// </summary>
    public async Task<Dictionary<string, string>> ResolveMappingsAsync(Dictionary<string, string> mappings, MessageContext? context = null)
    {
        var result = new Dictionary<string, string>();

        if (mappings == null || mappings.Count == 0)
            return result;

        foreach (var (varName, source) in mappings)
        {
            try
            {
                var resolvedValue = await ResolveAsync(source, context);
                result[varName] = resolvedValue ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve variable {VarName} from source {Source}", varName, source);
                result[varName] = string.Empty;
            }
        }

        return result;
    }

    /// <summary>
    /// Resolve from Vault (Bitwarden)
    /// Implementation TBD - will use bw CLI or Vault API
    /// </summary>
    private Task<string> ResolveFromVaultAsync(string itemName, MessageContext? context)
    {
        // TODO: Implement vault resolution
        // - Use bw CLI to fetch item
        // - Parse username/password/custom fields
        _logger.LogInformation("Resolving from Vault: {ItemName}", itemName);
        return Task.FromResult(string.Empty);
    }

    /// <summary>
    /// Resolve from Environment Variables
    /// </summary>
    private string ResolveFromEnvironment(string varName)
    {
        var value = Environment.GetEnvironmentVariable(varName);

        if (string.IsNullOrEmpty(value))
        {
            _logger.LogWarning("Environment variable not found: {VarName}", varName);
            return string.Empty;
        }

        _logger.LogInformation("Resolved environment variable: {VarName}", varName);
        return value;
    }
}
