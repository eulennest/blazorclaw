using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Tools;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Model;

public class ModelSwitchParams
{
    [Description("Kurzname des Modells: mistral, gemini, llama, gpt4, gpt4o, gpt4o-mini")]
    [Required]
    public string Model { get; set; } = string.Empty;
}

public class ModelSwitchTool : BaseTool<ModelSwitchParams>
{
    private readonly IOptionsMonitor<LlmOptions> _optionsMonitor;
    private readonly IProviderManager _providerManager;

    // Mapping from short names to full model identifiers
    private static readonly Dictionary<string, string> ModelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "mistral", "openrouter/mistralai/mistral-large" },
        { "mistral-large", "openrouter/mistralai/mistral-large" },
        { "gemini", "openrouter/google/gemini-2.0-flash-exp" },
        { "gemini-flash", "openrouter/google/gemini-2.0-flash-exp" },
        { "llama", "openrouter/meta-llama/llama-3.3-70b-instruct" },
        { "llama-70b", "openrouter/meta-llama/llama-3.3-70b-instruct" },
        { "gpt4", "openrouter/openai/gpt-4" },
        { "gpt4o", "openrouter/openai/gpt-4o" },
        { "gpt4o-mini", "openrouter/openai/gpt-4o-mini" },
        { "claude", "openrouter/anthropic/claude-sonnet-4-20250514" },
        { "claude-sonnet", "openrouter/anthropic/claude-sonnet-4-20250514" },
    };

    public ModelSwitchTool(IOptionsMonitor<LlmOptions> optionsMonitor, IProviderManager providerManager)
    {
        _optionsMonitor = optionsMonitor;
        _providerManager = providerManager;
    }

    public override string Name => "model_switch";
    public override string Description => "Wechselt schnell zu einem anderen Modell (Kurzname)";

    protected override Task<string> ExecuteInternalAsync(ModelSwitchParams p, MessageContext context)
    {
        var shortName = p.Model.ToLowerInvariant().Trim();
        
        // Check if short name exists in map
        if (!ModelMap.TryGetValue(shortName, out var fullModel))
        {
            var available = string.Join(", ", ModelMap.Keys);
            return Task.FromResult($"Unbekanntes Modell '{p.Model}'. Verfügbare Modelle: {available}");
        }

        // Validate provider exists
        var providerName = fullModel.Split('/')[0];
        var availableProviders = _providerManager.GetProviders().ToList();
        
        if (!availableProviders.Contains(providerName, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult($"Fehler: Provider '{providerName}' nicht konfiguriert.");

        // Set the model
        _optionsMonitor.CurrentValue.Model = fullModel;
        
        return Task.FromResult($"Modell gewechselt zu: {fullModel}");
    }
}
