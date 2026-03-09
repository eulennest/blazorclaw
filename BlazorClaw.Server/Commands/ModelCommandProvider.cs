using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Sessions;
using Microsoft.Extensions.Options;
using System.CommandLine;

namespace BlazorClaw.Server.Commands;

public class ModelCommandProvider : ExecutorCommandProvider
{
    public override IEnumerable<ISystemCommand> GetCommands()
    {
        yield return new ModelGetCommand();
        yield return new ModelSwitchCommand();
    }
}

public class ModelGetCommand : ISystemCommand, ISystemCommandExecutor
{
    public Command GetCommand() => new("model", "Zeigt LLM-Einstellungen");

    public Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        var options = context.Provider.GetRequiredService<IOptionsMonitor<LlmOptions>>().CurrentValue;
        return Task.FromResult<object?>($"Model: {options.Model}\nTemperature: {options.Temperature}\nMaxTokens: {options.MaxTokens}");
    }
}

public class ModelSwitchCommand : ISystemCommand, ISystemCommandExecutor
{
    private static readonly Dictionary<string, string> ModelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "mistral", "openrouter/mistralai/mistral-large" },
        { "gemini", "openrouter/google/gemini-2.0-flash-exp" },
        { "llama", "openrouter/meta-llama/llama-3.3-70b-instruct" },
        { "gpt4", "openrouter/openai/gpt-4" },
        { "gpt4o", "openrouter/openai/gpt-4o" },
        { "gpt4o-mini", "openrouter/openai/gpt-4o-mini" },
        { "claude", "openrouter/anthropic/claude-sonnet-4-20250514" },
    };

    public Command GetCommand()
    {
        var cmd = new Command("model", "Wechselt das Modell");
        cmd.Add(new Argument<string>("model") { Description = "Kurzname: mistral, gemini, llama, gpt4, gpt4o, claude" });
        return cmd;
    }

    public Task<object?> ExecuteAsync(ParseResult result, MessageContext context)
    {
        string input;
        try
        {
            input = result.GetRequiredValue<string>((Argument<string>)result.CommandResult.Command.Arguments[0]);
        }
        catch
        {
            return Task.FromResult<object?>($"Verfügbare Modelle: {string.Join(", ", ModelMap.Keys)}\nUsage: /model <modell>");
        }
        
        var providerManager = context.Provider.GetRequiredService<IProviderManager>();
        
        // Check if full model name (contains '/')
        string fullModel;
        if (input.Contains('/'))
        {
            fullModel = input;
        }
        else if (ModelMap.TryGetValue(input.ToLowerInvariant(), out var mapped))
        {
            fullModel = mapped;
        }
        else
        {
            return Task.FromResult<object?>($"Unbekannt. Verfügbare Kurznamen: {string.Join(", ", ModelMap.Keys)}");
        }

        var providerName = fullModel.Split('/')[0];
        var availableProviders = providerManager.GetProviders().ToList();
        
        if (!availableProviders.Contains(providerName, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult<object?>($"Fehler: Provider '{providerName}' nicht konfiguriert.");

        context.Provider.GetRequiredService<IOptionsMonitor<LlmOptions>>().CurrentValue.Model = fullModel;
        
        return Task.FromResult<object?>($"Gewechselt zu: {fullModel}");
    }
}
