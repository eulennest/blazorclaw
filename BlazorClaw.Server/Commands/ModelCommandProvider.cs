using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Providers;
using Microsoft.EntityFrameworkCore;
using System.CommandLine;

namespace BlazorClaw.Server.Commands;

public class ModelCommandProvider : ExecutorCommandProvider
{
    public override IEnumerable<ISystemCommand> GetCommands()
    {
        yield return new ModelSwitchCommand();
    }
}

public class ModelSwitchCommand : ISystemCommand, ISystemCommandExecutor
{
    private Dictionary<string, string>? modelMap;

    public Command GetCommand()
    {
        var cmd = new Command("model", "Wechselt das Modell");
        cmd.Add(new Argument<string>("model") { Description = "Kurzname: mistral, gemini, llama, gpt4, gpt4o, claude" });
        return cmd;
    }

    public async Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        var input = result.GetValue<string>((Argument<string>)result.CommandResult.Command.Arguments[0]);
        if (string.IsNullOrWhiteSpace(input))
        {
            return $"Aktuelles Model: {context.Session?.CurrentModel}";
        }

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

        var model = await SearchModelAsync(input, context) ?? throw new Exception($"Model nicht gefunden.");
        // Set the model on session
        context.Session.CurrentModel = model;
        return $"Modell gewechselt zu: {model}";
    }

    public async Task<string?> SearchModelAsync(string searchTerm, MessageContext context)
    {
        var _providerManager = context.Provider.GetRequiredService<IProviderManager>();
        // Check if search term exists in map
        if (modelMap!.TryGetValue(searchTerm, out var fullModel)) return fullModel;

        // Validate provider exists
        var cols = searchTerm.Split('/', 2);
        var providerName = cols[0];
        var modelName = cols.Length > 1 ? cols[1] : cols[0];
        var availableProviders = _providerManager.GetProviders().ToList();

        if (availableProviders.Contains(providerName, StringComparer.OrdinalIgnoreCase))
        {
            var exists = await _providerManager.GetModelsAsync(providerName).ContainsAsync(modelName);
            if (exists) return searchTerm;
        }
        var list = await _providerManager.GetModelsAsync().ToListAsync();

        foreach (var model in list)
        {
            if (model.EndsWith(modelName)) return model;
        }
        return null;
    }
}
