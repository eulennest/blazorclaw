using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BlazorClaw.Core.Utils;
using Microsoft.AspNetCore.Http;

namespace BlazorClaw.Core.Tools;

public class ToolContext
{
    public Guid SessionId { get; set; }
    public HttpContext? HttpContext { get; set; }
    public string? UserId { get; set; }
    public IServiceProvider ServiceProvider { get; set; } = default!;
}

public interface ITool
{
    string Name { get; }
    string Description { get; }
    object GetSchema();
    object BuidlArguments(string arguments);
    Task<string> ExecuteAsync(object arguments, ToolContext context);
}

public abstract class BaseTool<TParams> : ITool where TParams : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    
    public object GetSchema() => SchemaGenerator.Generate(typeof(TParams));
    public object BuidlArguments(string arguments) => JsonSerializer.Deserialize<TParams>(arguments, JsonHelper.DefaultOptions)!;

    public async Task<string> ExecuteAsync(object arguments, ToolContext context)
    {
        if (arguments is not TParams deserializedParams)
            throw new ArgumentException("Invalid arguments provided.");

        // Modell-Validierung (DataAnnotations)
        var validationContext = new ValidationContext(deserializedParams);
        Validator.ValidateObject(deserializedParams, validationContext, validateAllProperties: true);
        
        return await ExecuteInternalAsync(deserializedParams, context);
    }

    protected abstract Task<string> ExecuteInternalAsync(TParams parameters, ToolContext context);
}

public class EmptyParams { }
