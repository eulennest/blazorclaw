using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Model;

public class ModelSwitchParams
{
    [Description("Kurzname, Alias oder Name des Modells")]
    [Required]
    public string Model { get; set; } = string.Empty;
}

public class ModelSwitchTool : BaseTool<ModelSwitchParams>
{
    private readonly IOptionsMonitor<LlmOptions> _optionsMonitor;
    private readonly IProviderManager _providerManager;
    private readonly IServiceScopeFactory _scopeFactory;

    // Fallback model map for when database is not available
    private static readonly Dictionary<string, string> FallbackModelMap = new(StringComparer.OrdinalIgnoreCase)
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

    public ModelSwitchTool(
        IOptionsMonitor<LlmOptions> optionsMonitor, 
        IProviderManager providerManager,
        IServiceScopeFactory scopeFactory)
    {
        _optionsMonitor = optionsMonitor;
        _providerManager = providerManager;
        _scopeFactory = scopeFactory;
    }

    public override string Name => "model_switch";
    public override string Description => "Wechselt schnell zu einem anderen Modell (Kurzname, Alias oder Name)";

    protected override async Task<string> ExecuteInternalAsync(ModelSwitchParams p, MessageContext context)
    {
        if (context.Session == null)
            return "Fehler: Keine Session verfügbar";

        var searchTerm = p.Model.ToLowerInvariant().Trim();
        
        // Build model map from database favorites
        var modelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var favorites = await db.ModelFavorites.ToListAsync();
            
            foreach (var fav in favorites)
            {
                // Add InternalName as key
                var internalKey = fav.InternalName.ToLowerInvariant();
                modelMap[internalKey] = fav.InternalName;
                
                // Add Name as key (lowercase)
                if (!string.IsNullOrEmpty(fav.Name))
                {
                    modelMap[fav.Name.ToLowerInvariant()] = fav.InternalName;
                }
                
                // Add all aliases
                foreach (var alias in fav.Aliases)
                {
                    if (!string.IsNullOrEmpty(alias))
                    {
                        modelMap[alias.ToLowerInvariant()] = fav.InternalName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback to static map if database fails
            foreach (var kvp in FallbackModelMap)
            {
                modelMap[kvp.Key] = kvp.Value;
            }
        }

        // If no favorites found, use fallback
        if (!modelMap.Any())
        {
            foreach (var kvp in FallbackModelMap)
            {
                modelMap[kvp.Key] = kvp.Value;
            }
        }
        
        // Check if search term exists in map
        if (!modelMap.TryGetValue(searchTerm, out var fullModel))
        {
            var available = string.Join(", ", modelMap.Keys);
            return $"Unbekanntes Modell '{p.Model}'. Verfügbare Modelle: {available}";
        }

        // Validate provider exists
        var providerName = fullModel.Split('/')[0];
        var availableProviders = _providerManager.GetProviders().ToList();
        
        if (!availableProviders.Contains(providerName, StringComparer.OrdinalIgnoreCase))
            return $"Fehler: Provider '{providerName}' nicht konfiguriert.";

        // Set the model on session
        context.Session.CurrentModel = fullModel;
        
        return $"Modell gewechselt zu: {fullModel}";
    }
}