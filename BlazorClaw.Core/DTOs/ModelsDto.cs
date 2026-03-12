using System.Text.Json.Serialization;

namespace BlazorClaw.Core.DTOs;

/// <summary>
/// Response from GET /v1/models (OpenAI-compatible)
/// </summary>
public class ModelListResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "list";

    [JsonPropertyName("data")]
    public List<ModelInfo> Data { get; set; } = [];
}

/// <summary>
/// Individual model information
/// </summary>
public class ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "model";

    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("pricing")]
    public ModelPricing? Pricing { get; set; }

    [JsonPropertyName("context_window")]
    public long? ContextWindow { get; set; }

    [JsonPropertyName("max_tokens")]
    public long? MaxTokens { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("architecture")]
    public ModelArchitecture? Architecture { get; set; }

    [JsonPropertyName("modality")]
    public string? Modality { get; set; }

    [JsonPropertyName("finetuning")]
    public ModelFinetuning? Finetuning { get; set; }

    [JsonPropertyName("parameters")]
    public long? Parameters { get; set; }

    [JsonPropertyName("provider_name")]
    public string? ProviderName { get; set; }

    [JsonPropertyName("provider_region")]
    public string? ProviderRegion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    /// <summary>
    /// Returns a clean display name (id if display_name is not available)
    /// </summary>
    public string GetDisplayName() => !string.IsNullOrEmpty(DisplayName) ? DisplayName : Id;
}

/// <summary>
/// Pricing information for a model
/// </summary>
public class ModelPricing
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("completion")]
    public string? Completion { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("tts")]
    public string? Tts { get; set; }

    [JsonPropertyName("rtxt")]
    public string? Rtxt { get; set; }

    [JsonPropertyName("embedding")]
    public string? Embedding { get; set; }
}

/// <summary>
/// Model architecture details
/// </summary>
public class ModelArchitecture
{
    [JsonPropertyName("modality")]
    public string? Modality { get; set; }

    [JsonPropertyName("tokenizer")]
    public string? Tokenizer { get; set; }

    [JsonPropertyName("instruction_type")]
    public string? InstructionType { get; set; }
}

/// <summary>
/// Finetuning capabilities
/// </summary>
public class ModelFinetuning
{
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    [JsonPropertyName("hyperparameters")]
    public FinetuningHyperparameters? Hyperparameters { get; set; }
}

public class FinetuningHyperparameters
{
    [JsonPropertyName("batch_size")]
    public object? BatchSize { get; set; }

    [JsonPropertyName("learning_rate")]
    public object? LearningRate { get; set; }

    [JsonPropertyName("epochs")]
    public object? Epochs { get; set; }
}
