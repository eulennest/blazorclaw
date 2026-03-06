using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BlazorClaw.Core.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    object GetSchema(); 
    Task<string> ExecuteAsync(string argumentsJson);
}

public abstract class BaseTool<TParams> : ITool where TParams : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    
    public override object GetSchema() => SchemaGenerator.Generate(typeof(TParams));

    public async Task<string> ExecuteAsync(string argumentsJson)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var deserializedParams = JsonSerializer.Deserialize<TParams>(argumentsJson, options);
        
        if (deserializedParams == null)
            throw new ArgumentException("Invalid arguments provided.");

        // Modell-Validierung (DataAnnotations)
        var validationContext = new ValidationContext(deserializedParams);
        Validator.ValidateObject(deserializedParams, validationContext, validateAllProperties: true);
        
        return await ExecuteInternalAsync(deserializedParams);
    }

    protected abstract Task<string> ExecuteInternalAsync(TParams parameters);
}
