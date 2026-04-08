using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using BlazorClaw.Server.Tools.Mcp;
using ModelContextProtocol.Client;
using Newtonsoft.Json;

namespace BlazorClaw.Server.Tools;

public class McpToolRegistry(IVfsSystem vfs) : IToolProvider
{
    private Dictionary<McpServerEntry, IList<McpClientTool>>? ToolsReg;
    private List<ITool>? Tools;
    public async IAsyncEnumerable<ITool> GetAllToolsAsync()
    {
        if (Tools == null)
        {
            ToolsReg ??= await BuildAllToolsAsync();
            Tools = [.. ToolsReg.SelectMany(kv => kv.Value.Select(u => Tuple.Create(kv.Key, u))).Select(tool => (ITool)new McpTool(tool.Item1, tool.Item2))];
        }

        foreach (var item in Tools ?? [])
        {
            yield return item;
        }
    }

    public async Task<Dictionary<McpServerEntry, IList<McpClientTool>>> BuildAllToolsAsync()
    {
        var ret = new Dictionary<McpServerEntry, IList<McpClientTool>>();
        var regs = await McpRegistry.LoadRegistryAsync(vfs, PathUtils.VfsMcpUser);
        foreach (var reg in regs.Servers)
        {
            var uri = new Uri(reg.ServerUri, UriKind.Absolute);
            var transport = FromUri(uri);
            if (transport == null) continue;
            await using var mcpClient = await McpClient.CreateAsync(transport);
            var tools = await mcpClient.ListToolsAsync();
            ret[reg] = tools;
        }
        return ret;
    }

    public ITool? GetTool(string name) => Tools?.FirstOrDefault(o => o.Name.Equals(name));


    internal static IClientTransport? FromUri(Uri uri)
    {
        if (uri.Scheme == "http" || uri.Scheme == "https")
        {
            return new HttpClientTransport(new()
            {
                Endpoint = uri
            });
        }
        return null;
    }

    private class McpTool(McpServerEntry entry, McpClientTool tool) : ITool
    {
        public string Name => $"mcp-{entry.Name}-" + tool.Name.Replace(".", "___");
        public string Description => tool.Description ?? string.Empty;

        public object BuildArguments(string arguments)
        {
            return arguments;
        }

        public async Task<string> ExecuteAsync(object arguments, MessageContext context)
        {
            var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(arguments.ToString() ?? string.Empty) ?? new Dictionary<string, object>();
            var uri = new Uri(entry.ServerUri, UriKind.Absolute);
            var transport = McpToolRegistry.FromUri(uri) ?? throw new Exception(Name + " has invalid transport");
            await using var mcpClient = await McpClient.CreateAsync(transport);
            var ret = await mcpClient.CallToolAsync(tool.Name, args);
            return ret.StructuredContent?.ToString() ?? string.Empty;
        }

        public object GetSchema()
        {
            return tool.JsonSchema;
        }
    }
}