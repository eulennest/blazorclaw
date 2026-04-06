using BlazorClaw.Core.Commands;

namespace BlazorClaw.Core.Services
{
    /// <summary>
    /// Resolves variables from different sources (Vault, Environment, etc.)
    /// </summary>
    public interface IVariableResolver
    {
        /// <summary>
        /// Resolve a variable mapping entry (e.g., "vault:Home_Assistant_Token" → actual token)
        /// </summary>
        /// <param name="source">Source specification (e.g., "vault:ItemName" or "env:VAR_NAME")</param>
        /// <param name="context">Message context for vault/environment access</param>
        /// <returns>Resolved value or empty string if not found</returns>
        Task<string> ResolveAsync(string source, MessageContext context);

        /// <summary>
        /// Resolve all mappings and return as dictionary for substitution
        /// </summary>
        /// <param name="mappings">Dictionary with VAR_NAME → source:item_name mappings</param>
        /// <param name="context">Message context for vault/environment access</param>
        /// <returns>Dictionary with VAR_NAME → resolved_value</returns>
        Task<Dictionary<string, string>> ResolveMappingsAsync(Dictionary<string, string> mappings, MessageContext context);

    }
}
