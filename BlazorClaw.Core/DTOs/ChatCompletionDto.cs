using System.Text.Json.Serialization;

namespace BlazorClaw.Core.DTOs;

public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("tools")]
    public List<FunctionMessage>? Tools { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public virtual string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public virtual object? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public virtual List<ToolCall>? ToolCalls { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    [JsonIgnore]
    public bool IsAssistant => Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsTool => Role.Equals("tool", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsSystem => Role.Equals("system", StringComparison.OrdinalIgnoreCase);

    public static ChatMessage Build(string content)
    {
        return new ChatMessage { Role = "user", Content = content };
    }
    public static ChatMessage Build(Exception ex)
    {
        return new ChatMessage { Role = "error", Content = $"Error: {ex.Message}" };
    }
}

public class ToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionCall Function { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public class FunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public class ToolDefinition
{

    [JsonPropertyName("parameters")]
    public object Parameters { get; set; } = new();

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
public class FunctionMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public required ToolDefinition Function { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}


public class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; set; } = [];

    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }

    [JsonPropertyName("usage")]
    public TokenUsage? Usage { get; set; }


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

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

public class TokenUsage
{
    [JsonPropertyName("prompt_tokens")]
    public double PromptTokens { get; set; }
    [JsonPropertyName("completion_tokens")]
    public double CompletionTokens { get; set; }
    [JsonPropertyName("total_tokens")]
    public double TotalTokens { get; set; }
    [JsonPropertyName("cost")]
    public double? PromptCost { get; set; }
}

public class ApiError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public object? Code { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
