using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace BlazorClaw.Core.Tools;

public class ToolContext
{
    public Guid SessionId { get; set; }
    public HttpContext? HttpContext { get; set; }
    public string UserId { get; set; } = string.Empty;
    public IServiceProvider ServiceProvider { get; set; } = default!;
}

public interface ITool
{
    string Name { get; }
    string Description { get; }
    object GetSchema(); 
    Task<string> ExecuteAsync(string argumentsJson, ToolContext context);
}

public abstract class BaseTool<TParams> : ITool where TParams : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    
    public object GetSchema() => SchemaGenerator.Generate(typeof(TParams));

    public async Task<string> ExecuteAsync(string argumentsJson, ToolContext context)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var deserializedParams = JsonSerializer.Deserialize<TParams>(argumentsJson, options);
        
        if (deserializedParams == null)
            throw new ArgumentException("Invalid arguments provided.");

        // Modell-Validierung (DataAnnotations)
        var validationContext = new ValidationContext(deserializedParams);
        Validator.ValidateObject(deserializedParams, validationContext, validateAllProperties: true);
        
        return await ExecuteInternalAsync(deserializedParams, context);
    }

    protected abstract Task<string> ExecuteInternalAsync(TParams parameters, ToolContext context);
}
