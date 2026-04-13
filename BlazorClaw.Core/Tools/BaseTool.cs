using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Utils;
using Microsoft.Extensions.AI;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BlazorClaw.Core.Tools;


public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonElement GetSchema();
    object? BuildArguments(object? arguments);
    Task<string> ExecuteAsync(object? arguments, MessageContext context);
}

public abstract class BaseTool<TParams> : ITool where TParams : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public override string ToString() => $"{Name}: {Description}";

    // public JsonElement GetSchema() => SchemaGenerator.Generate(typeof(TParams));
    public JsonElement GetSchema() => AIJsonUtilities.CreateJsonSchema(typeof(TParams));
    public object? BuildArguments(object? arguments)
    {
        if (arguments == null)
            return null;

        if (arguments is TParams typed)
            return typed;

        if (arguments is JsonElement json)
            return json.Deserialize<TParams>(JsonHelper.DefaultOptions);

        if (arguments is string str)
            return JsonSerializer.Deserialize<TParams>(str, JsonHelper.DefaultOptions);

        var element = JsonSerializer.SerializeToElement(arguments, JsonHelper.DefaultOptions);
        return element.Deserialize<TParams>(JsonHelper.DefaultOptions);
    }

    public async Task<string> ExecuteAsync(object? arguments, MessageContext context)
    {
        if (arguments is not TParams deserializedParams)
            throw new ArgumentException("Invalid arguments provided.");

        // Modell-Validierung (DataAnnotations)
        var validationContext = new ValidationContext(deserializedParams);
        Validator.ValidateObject(deserializedParams, validationContext, validateAllProperties: true);

        return await ExecuteInternalAsync(deserializedParams, context);
    }

    protected abstract Task<string> ExecuteInternalAsync(TParams parameters, MessageContext context);
}

public class EmptyParams { }
