# CODING.md - BlazorClaw Development Standards

## Tool Design Patterns

### 1. Exception-Based Error Handling

**ALWAYS throw exceptions in Tool.ExecuteInternalAsync() — NEVER return error strings.**

```csharp
// ✅ CORRECT
public override async Task<string> ExecuteInternalAsync(MyParams p, MessageContext context)
{
    if (string.IsNullOrWhiteSpace(p.RequiredField))
        throw new ArgumentException("Field is required.", nameof(p.RequiredField));
    
    if (!Uri.TryCreate(p.Url, UriKind.Absolute, out _))
        throw new ArgumentException("Invalid URL format.", nameof(p.Url));
    
    if (!await CheckAccess(p))
        throw new InvalidOperationException("Access denied for this resource.");
    
    try
    {
        // Tool logic
    }
    catch (HttpRequestException ex)
    {
        throw new InvalidOperationException("HTTP request failed.", ex);
    }
}

// ❌ WRONG
return "ERROR: Field is required";
return $"ERROR: Access denied";
```

### Why Exceptions?

1. **ToolDispatcher catches exceptions** and converts to ProblemDetails JSON
2. **LLM receives structured error info:**
   - Exception type (ArgumentException, InvalidOperationException, NotImplementedException, etc.)
   - Message (exact error description)
   - Parameter name (via `nameof()`)
3. **Audit log records errors properly:**
   - What went wrong (exception type)
   - Why (message)
   - Which parameter caused it (nameof reference)
4. **Consistent error handling** across all tools

### Exception Types to Use

```csharp
// User input validation
throw new ArgumentException("Description", nameof(parameter));
throw new ArgumentOutOfRangeException("Description", nameof(parameter));
throw new FormatException("Invalid format in field X");

// Authorization & state
throw new InvalidOperationException("Current state doesn't allow this operation");
throw new UnauthorizedAccessException("User lacks permission");

// Not implemented yet
throw new NotImplementedException("Feature X is planned for v2.0");

// Unsupported scenarios
throw new NotSupportedException("Scheme 'xyz://' is not supported");

// External service failures
throw new HttpRequestException("HTTP request failed", innerException);
throw new TimeoutException("Service timeout after 30 seconds");
```

### ProblemDetails JSON Response (Auto-Generated)

When a tool throws an exception, ToolDispatcher converts it to:

```json
{
  "type": "https://www.rfc-7231.org/7231-6-6-1",
  "title": "ArgumentException",
  "status": 400,
  "detail": "Field is required.",
  "instance": "tool_name",
  "parameterName": "fieldName"
}
```

The LLM can then:
- Understand what went wrong (exception type + message)
- Adjust the request for the next attempt
- Inform the user with the exact error

---

## Tool Parameter Patterns

### 2. Nullable Properties for Optional Parameters

**Optional parameters MUST be nullable — SchemaGenerator uses nullability to detect optionality.**

```csharp
public class MyToolParams : BaseToolParams
{
    [Required]
    [Description("Required field")]
    public string Name { get; set; } = string.Empty;

    // ✅ CORRECT - Optional (nullable)
    [Description("Optional field")]
    public string? Description { get; set; }
    
    [Description("Optional timeout")]
    public int? TimeoutSeconds { get; set; } = 30;
    
    [Description("Optional flag")]
    public bool? IgnoreErrors { get; set; } = false;

    // ❌ WRONG - Not recognized as optional
    public string OptionalField { get; set; } = string.Empty;
}
```

### SchemaGenerator Rules

| C# Type | JSON Schema | Optional? |
|---------|------------|-----------|
| `string` | `"type": "string"` | ❌ Required |
| `string?` | `"type": ["string", "null"]` | ✅ Optional |
| `int` | `"type": "integer"` | ❌ Required |
| `int?` | `"type": ["integer", "null"]` | ✅ Optional |
| `bool` | `"type": "boolean"` | ❌ Required |
| `bool?` | `"type": ["boolean", "null"]` | ✅ Optional |
| `Dictionary<K,V>` | `"type": "object", "additionalProperties": {...}` | Depends on `?` |

---

## Tool Inheritance & Base Classes

### 3. BaseTool<TParams> Pattern

```csharp
using BlazorClaw.Core.Tools;

public class MyTool(ILogger<MyTool> logger) : BaseTool<MyParams>
{
    public override string Name => "my_tool";
    
    public override string Description => """
        Clear, concise description of what the tool does.
        Include examples of expected input/output.
        """;

    protected override async Task<string> ExecuteInternalAsync(MyParams p, MessageContext context)
    {
        // Resolve variables (automatic @VAR_NAME substitution)
        await p.ResolveVarsAsync(context);

        // Validate input → throw exceptions
        if (string.IsNullOrWhiteSpace(p.Field))
            throw new ArgumentException("Field is required.", nameof(p.Field));

        // Execute tool logic
        var result = await DoWork(p);

        // Return serialized result (string)
        return result;
    }

    private async Task<string> DoWork(MyParams p)
    {
        // Implementation
        return "result";
    }
}
```

### Variable Resolution (Automatic)

All `BaseToolParams` support variable substitution:

```csharp
p.ResolveVarsAsync(context)
```

This automatically replaces `@VAR_NAME` placeholders with values from:
- `VariableMappings` (e.g., `{"API_KEY": "vault:MyApiKey"}`)
- Environment variables
- Vault secrets

---

## HTTP & Socket Tools

### 4. HttpClient Factory Pattern

```csharp
public class MyHttpTool(IHttpClientFactory httpClientFactory) : BaseTool<MyParams>
{
    protected override async Task<string> ExecuteInternalAsync(MyParams p, MessageContext context)
    {
        await p.ResolveVarsAsync(context);

        // Use factory for proper HTTP client pooling
        var httpClient = httpClientFactory.CreateClient(
            p.IgnoreSslErrors ?? false ? "InsecureHttpClient" : "HttpClient"
        );
        httpClient.Timeout = TimeSpan.FromSeconds(p.TimeoutSeconds ?? 30);

        var response = await httpClient.PostAsync(p.Url, content);
        
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {response.StatusCode}: {response.ReasonPhrase}");

        return await response.Content.ReadAsStringAsync();
    }
}
```

### Socket Pattern (Unified for Unix/TCP/UDP)

```csharp
private async Task<string> CallViaSocketAsync(Socket socket, EndPoint endpoint, string request)
{
    using var sock = socket;
    await sock.ConnectAsync(endpoint);

    // Send
    var requestBytes = Encoding.UTF8.GetBytes(request + "\n");
    await sock.SendAsync(requestBytes, SocketFlags.None);

    // Receive
    var buffer = new byte[1024 * 64];
    int bytesRead = await sock.ReceiveAsync(buffer, SocketFlags.None);

    if (bytesRead == 0)
        throw new InvalidOperationException("Socket returned no data.");

    return Encoding.UTF8.GetString(buffer, 0, bytesRead);
}
```

### Supported Schemas

- `http://`, `https://` → HttpClient
- `unix:///path/to/socket` → UnixDomainSocketEndPoint
- `tcp://host:port` → IPEndPoint (TCP)
- `udp://host:port` → IPEndPoint (UDP)

---

## Data Classes & Shared Patterns

### 5. Stateless Helper Methods

When multiple tools share common logic, extract to static helpers:

```csharp
// ✅ CORRECT - Shared data class with static helpers
public class MyRegistry
{
    public List<MyEntry> Entries { get; set; } = new();

    // Static helpers for all tools to use
    public static async Task<MyRegistry> LoadAsync(IVfsSystem vfs)
    {
        var filePath = VfsPath.Parse("/~secure/my-registry.json");
        if (!await vfs.ExistsAsync(filePath))
            return new MyRegistry();

        using var stream = await vfs.OpenFileAsync(filePath, FileMode.Open, FileAccess.Read);
        return await JsonSerializer.DeserializeAsync<MyRegistry>(stream) ?? new MyRegistry();
    }

    public static async Task SaveAsync(MyRegistry registry, IVfsSystem vfs)
    {
        var filePath = VfsPath.Parse("/~secure/my-registry.json");
        var parentPath = filePath.ParentPath;
        
        if (!await vfs.ExistsAsync(parentPath))
            await vfs.CreateDirectoryAsync(parentPath);

        using var stream = await vfs.OpenFileAsync(filePath, FileMode.Create, FileAccess.Write);
        var options = new JsonSerializerOptions { WriteIndented = true };
        await JsonSerializer.SerializeAsync(stream, registry, options);
    }
}

// ✅ CORRECT - Tools use shared methods
public class MyListTool(IVfsSystem vfs) : BaseTool<MyListParams>
{
    protected override async Task<string> ExecuteInternalAsync(MyListParams p, MessageContext context)
    {
        var registry = await MyRegistry.LoadAsync(vfs);
        // Use registry data
    }
}
```

---

## Folder Organization

### 6. Tool Grouping by Domain

Tools that belong together should be in subdirectories:

```
/BlazorClaw.Server/Tools/
├── Mcp/                    ← MCP domain
│   ├── McpCallTool.cs
│   ├── McpListTool.cs
│   ├── McpSetTool.cs
│   └── McpShared.cs
├── HttpRequestTool.cs      ← Standalone HTTP
├── ExecTool.cs             ← Standalone Exec
└── ...
```

**Namespace reflects folder structure:**
```csharp
namespace BlazorClaw.Server.Tools.Mcp;  // For /Tools/Mcp/
namespace BlazorClaw.Server.Tools;      // For /Tools/
```

---

## Logging Best Practices

### 7. When & What to Log

```csharp
protected override async Task<string> ExecuteInternalAsync(MyParams p, MessageContext context)
{
    // Log START with parameters (useful for debugging)
    logger.LogInformation("Tool starting with Url={Url}, Timeout={Timeout}", p.Url, p.TimeoutSeconds);

    try
    {
        var result = await DoWork(p);
        // Log SUCCESS only if operation is noteworthy
        logger.LogInformation("Tool completed successfully");
        return result;
    }
    catch (ArgumentException ex)
    {
        // Log INPUT ERRORS (user's fault, not tool's)
        logger.LogWarning(ex, "Invalid argument in tool execution");
        throw;  // Re-throw for ToolDispatcher to handle
    }
    catch (Exception ex)
    {
        // Log UNEXPECTED ERRORS (tool's fault)
        logger.LogError(ex, "Tool execution failed unexpectedly");
        throw new InvalidOperationException("Tool failed.", ex);
    }
}
```

**Don't log:**
- Success messages (unless critical for audit)
- Passwords, tokens, or sensitive data
- Full response bodies (use truncated versions)

---

## Summary Checklist

Before submitting a tool PR:

- [ ] Uses **exceptions** for errors (not error strings)
- [ ] Optional parameters are **nullable** (`bool?`, `int?`, `string?`)
- [ ] Has `[Required]` and `[Description]` attributes
- [ ] Calls `await p.ResolveVarsAsync(context)` at the start
- [ ] Uses `IHttpClientFactory` (not `new HttpClient()`)
- [ ] Organized in appropriate folder/namespace
- [ ] Logging is minimal and doesn't leak secrets
- [ ] Throws appropriate exception types (not generic `Exception`)
- [ ] Includes clear examples in `Description`

---

See also:
- `/BlazorClaw.Server/Tools/Mcp/` — Reference implementation
- `SECURITY.md` — Authentication & Vault integration
