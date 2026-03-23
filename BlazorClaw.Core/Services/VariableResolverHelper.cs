using BlazorClaw.Core.Commands;
using System.Text.RegularExpressions;

namespace BlazorClaw.Core.Services;

/// <summary>
/// Helper for variable resolution and placeholder replacement
/// </summary>
public class VariableResolverHelper
{
    private readonly IVariableResolver _resolver;

    public VariableResolverHelper(IVariableResolver resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// Replace all @VAR_NAME placeholders with resolved values
    /// </summary>
    /// <param name="text">Text containing @VAR_NAME placeholders</param>
    /// <param name="variables">Dictionary with VAR_NAME → value</param>
    /// <returns>Text with variables replaced</returns>
    public string ReplacePlaceholders(string text, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(text) || variables == null || variables.Count == 0)
            return text;

        var result = text;
        
        // Replace @VAR_NAME with corresponding values
        foreach (var (varName, varValue) in variables)
        {
            result = Regex.Replace(result, $@"@{Regex.Escape(varName)}\b", varValue ?? string.Empty);
        }

        return result;
    }

    /// <summary>
    /// Resolve variable mappings asynchronously
    /// </summary>
    /// <param name="mappings">Dictionary with VAR_NAME → source:item_name mappings</param>
    /// <param name="context">Message context for vault/environment access</param>
    /// <returns>Dictionary with VAR_NAME → resolved_value</returns>
    public async Task<Dictionary<string, string>> ResolveMappingsAsync(Dictionary<string, string> mappings, MessageContext? context = null)
    {
        var result = new Dictionary<string, string>();

        if (mappings == null || mappings.Count == 0)
            return result;

        foreach (var (varName, source) in mappings)
        {
            var resolvedValue = await _resolver.ResolveAsync(source, context);
            result[varName] = resolvedValue ?? string.Empty;
        }

        return result;
    }

    /// <summary>
    /// Resolve a single variable source
    /// </summary>
    /// <param name="source">Source specification (e.g., "vault:Home_Assistant_Token" or "env:HA_TOKEN")</param>
    /// <param name="context">Message context for vault/environment access</param>
    /// <returns>Resolved value or empty string if not found</returns>
    public Task<string> ResolveAsync(string source, MessageContext? context = null)
    {
        return _resolver.ResolveAsync(source, context);
    }

    /// <summary>
    /// Process parameters with variable substitution
    /// </summary>
    /// <param name="text">Text with potential @VAR_NAME placeholders</param>
    /// <param name="mappings">Variable mappings to resolve</param>
    /// <param name="context">Message context</param>
    /// <returns>Processed text with variables replaced</returns>
    public async Task<string> ProcessAsync(string text, Dictionary<string, string>? mappings, MessageContext? context = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // If no mappings, return as-is
        if (mappings == null || mappings.Count == 0)
            return text;

        // Resolve all mappings
        var variables = await ResolveMappingsAsync(mappings, context);

        // Replace placeholders
        return ReplacePlaceholders(text, variables);
    }
}
