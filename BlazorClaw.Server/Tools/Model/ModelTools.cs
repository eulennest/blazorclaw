using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Server.Tools.Model;

public class ModelGetParams
{
    [Description("Optional: Welcher Wert soll abgerufen werden (model, temperature, maxTokens). Standard: all")]
    public string? Property { get; set; }
}

public class ModelSetParams
{
    [Description("Das zu verwendende Modell (z.B. openrouter/mistralai/mistral-large)")]
    public string? Model { get; set; }

    [Description("Temperatur (0.0 - 2.0)")]
    public double? Temperature { get; set; }

    [Description("Maximale Token")]
    public int? MaxTokens { get; set; }
}

public class ModelGetTool : BaseTool<ModelGetParams>
{
    private readonly LlmOptions _options;

    public ModelGetTool(IOptionsMonitor<LlmOptions> options)
    {
        _options = options.CurrentValue;
    }

    public override string Name => "model_get";
    public override string Description => "Holt die aktuellen LLM-Einstellungen (Modell, Temperature, MaxTokens)";

    protected override Task<string> ExecuteInternalAsync(ModelGetParams p, MessageContext context)
    {
        var prop = p.Property?.ToLowerInvariant();

        return Task.FromResult(prop switch
        {
            "model" => _options.Model,
            "temperature" => _options.Temperature.ToString(),
            "maxtokens" => _options.MaxTokens.ToString(),
            _ => $"Model: {_options.Model}\nTemperature: {_options.Temperature}\nMaxTokens: {_options.MaxTokens}"
        });
    }
}

public class ModelSetTool : BaseTool<ModelSetParams>
{
    private readonly IOptionsMonitor<LlmOptions> _optionsMonitor;

    public ModelSetTool(IOptionsMonitor<LlmOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public override string Name => "model_set";
    public override string Description => "Setzt die LLM-Einstellungen (Modell, Temperature, MaxTokens)";

    protected override Task<string> ExecuteInternalAsync(ModelSetParams p, MessageContext context)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!string.IsNullOrEmpty(p.Model))
            options.Model = p.Model;

        if (p.Temperature.HasValue)
            options.Temperature = p.Temperature.Value;

        if (p.MaxTokens.HasValue)
            options.MaxTokens = p.MaxTokens.Value;

        return Task.FromResult($"Einstellungen aktualisiert:\nModel: {options.Model}\nTemperature: {options.Temperature}\nMaxTokens: {options.MaxTokens}");
    }
}
