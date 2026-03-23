using System.ComponentModel;

namespace BlazorClaw.Core.Tools;

/// <summary>
/// Base class for tool parameters with variable substitution support
/// </summary>
public abstract class BaseToolParams
{
    [Description("Variable Mappings for substitution. Format: {\"VAR_NAME\": \"vault:ItemName\" or \"env:VAR_NAME\"}. Use @VAR_NAME in other parameters to substitute.")]
    public Dictionary<string, string>? VariableMappings { get; set; }
}
