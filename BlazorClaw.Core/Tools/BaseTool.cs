using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Utils;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BlazorClaw.Core.Tools;


public interface ITool
{
    string Name { get; }
    string Description { get; }
    object GetSchema();
    object BuildArguments(IDictionary<string, object?>? arguments);
    Task<string> ExecuteAsync(object arguments, MessageContext context);
}

public abstract class BaseTool<TParams> : ITool where TParams : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public object GetSchema() => SchemaGenerator.Generate(typeof(TParams));
    public object BuildArguments(IDictionary<string, object?>? arguments)
    {
        var str = JsonSerializer.Serialize(arguments, JsonHelper.DefaultOptions);
        return JsonSerializer.Deserialize<TParams>(str, JsonHelper.DefaultOptions)!;
    }

    public async Task<string> ExecuteAsync(object arguments, MessageContext context)
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
