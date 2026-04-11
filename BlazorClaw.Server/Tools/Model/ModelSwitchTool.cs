using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Tools;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Model;

public class ModelSwitchParams
{
    [Description("Kurzname, Alias oder Name des Modells")]
    [Required]
    public string Model { get; set; } = string.Empty;
}

public class ModelSwitchTool(IProviderManager providerManager) : BaseTool<ModelSwitchParams>
{
    private Dictionary<string, string>? modelMap;

    public override string Name => "model_switch";
    public override string Description => "Wechselt schnell zu einem anderen Modell (Kurzname, Alias oder Name)";

    protected override async Task<string> ExecuteInternalAsync(ModelSwitchParams p, MessageContext context)
    {
        if (context.Session == null)
            return "Fehler: Keine Session verfügbar";

        var searchTerm = p.Model.ToLowerInvariant().Trim();


        if (modelMap == null)
        {
            // Build model map from database favorites
            modelMap ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var db = context.Provider.GetRequiredService<ApplicationDbContext>();

                var favorites = await db.ModelFavorites.ToListAsync();

                foreach (var fav in favorites)
                {
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
            catch (Exception)
            {
            }
        }

        var model = await SearchModelAsync(searchTerm) ?? throw new Exception($"Model nicht gefunden.");

        // Set the model on session
        context.Session.CurrentModel = model;

        return $"Modell gewechselt zu: {model}";
    }

    public async Task<string?> SearchModelAsync(string searchTerm)
    {
        // Check if search term exists in map
        if (modelMap?.TryGetValue(searchTerm, out var fullModel) ?? false) return fullModel;

        // Validate provider exists
        var cols = searchTerm.Split('/', 2);
        var providerName = cols[0];
        var modelName = cols.Length > 1 ? cols[1] : cols[0];
        var availableProviders = providerManager.GetProviders().ToList();

        if (availableProviders.Any(p => p.Id.Equals(providerName, StringComparison.OrdinalIgnoreCase)))
        {
            var exists = await providerManager.GetModelsAsync(providerName).ContainsAsync(modelName);
            if (exists) return searchTerm;
        }
        var list = await providerManager.GetModelsAsync().ToListAsync();

        foreach (var model in list)
        {
            if (model.EndsWith(modelName)) return model;
        }
        return null;
    }

}