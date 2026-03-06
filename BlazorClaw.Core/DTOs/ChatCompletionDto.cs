using System.Text.Json.Serialization;

namespace BlazorClaw.Core.DTOs;

public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<ToolDefinition>? Tools { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionCall Function { get; set; } = new();
}

public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public object Function { get; set; } = new();
}

public class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; set; } = new();

    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public class ChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class ApiError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
