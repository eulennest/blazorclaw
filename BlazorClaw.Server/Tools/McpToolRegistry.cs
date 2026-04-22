using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using BlazorClaw.Server.Tools.Mcp;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BlazorClaw.Server.Tools;

public class McpToolRegistry(IVfsSystem vfs, ILogger<McpToolRegistry> logger, ApplicationDbContext db) : IToolProvider
{
    public ConcurrentDictionary<string, McpServer>? ToolsReg { get; private set; }

    private IEnumerable<McpTool> Tools => ToolsReg?.Where(o => o.Value.Active)
            .SelectMany(kv => kv.Value.ClientTools.Select(u => Tuple.Create(kv.Value, u)))
            .Select(tool => new McpTool(tool.Item1, tool.Item2)) ?? [];
    public async IAsyncEnumerable<ITool> GetAllToolsAsync()
    {
        ToolsReg ??= await BuildAllToolsAsync();
        foreach (var item in Tools)
            yield return item;
    }
    public async Task ReloadAsync()
    {
        ToolsReg = await BuildAllToolsAsync();
    }

    public async Task<ConcurrentDictionary<string, McpServer>> BuildAllToolsAsync()
    {
        var ret = ToolsReg ?? [];
        var regs = await McpRegistry.LoadRegistryAsync(vfs, PathUtils.VfsMcpUser);
        foreach (var reg in regs.Servers)
        {
            try
            {


                if (reg.Access == AccessState.Disabled)
                {
                    if (ret.TryRemove(reg.Name, out var t))
                        if (t.McpClient != null) await t.McpClient.DisposeAsync();
                    continue;
                }

                if (ret.ContainsKey(reg.Name)) continue;

                var uri = new Uri(reg.ServerUri, UriKind.Absolute);
                var transport = FromUri(reg, uri);
                if (transport == null) continue;
                var mcpClient = await McpClient.CreateAsync(transport);
                var tools = await mcpClient.ListToolsAsync();
                ret[reg.Name] = new McpServer(reg) { McpClient = mcpClient, ClientTools = tools };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while building MCP tools for server {ServerName}", reg.Name);
            }
        }
        return ret;
    }

    public ITool? GetTool(string name) => Tools?.FirstOrDefault(o => o.Name.Equals(name));

    internal IClientTransport? FromUri(McpServerEntry reg, Uri uri)
    {
        if (uri.Scheme == "http" || uri.Scheme == "https")
        {

            var opts = new HttpClientTransportOptions()
            {
                Endpoint = uri,
            };
            if (reg.AuthType.Equals("oauth", StringComparison.InvariantCultureIgnoreCase))
            {
                opts.OAuth = new()
                {
                    RedirectUri = new Uri($"http://localhost:1179/mcp/{reg.Name}/callback"),
                    AuthorizationRedirectDelegate = HandleAuthorizationUrlAsync,
                    DynamicClientRegistration = new()
                    {
                        ClientName = "ProtectedMcpClient",
                    },
                };
            }


            return new HttpClientTransport(opts);
        }
        return null;
    }

    private async Task<string?> HandleAuthorizationUrlAsync(Uri authorizationUri, Uri redirectUri, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private class McpTool(McpServer entry, McpClientTool tool) : AIFunction, ITool
    {
        public override string Name => $"mcp_{entry.Entry.Name}_" + tool.Name.Replace(".", "_");
        public override string Description => tool.Description ?? string.Empty;
        public override JsonElement JsonSchema => tool.JsonSchema;
        public override JsonElement? ReturnJsonSchema => tool.ReturnJsonSchema;

        public object? BuildArguments(object? arguments)
        {
            if (arguments == null)
                return null;

            if (arguments is AIFunctionArguments typed)
                return typed;

            if (arguments is JsonElement json)
                return json.Deserialize<AIFunctionArguments>(JsonHelper.DefaultOptions);

            if (arguments is string str)
                return JsonSerializer.Deserialize<AIFunctionArguments>(str, JsonHelper.DefaultOptions);

            var element = JsonSerializer.SerializeToElement(arguments, JsonHelper.DefaultOptions);
            return element.Deserialize<AIFunctionArguments>(JsonHelper.DefaultOptions);
        }

        public async Task<string> ExecuteAsync(object? arguments, MessageContext context)
        {
            var args = BuildArguments(arguments) as AIFunctionArguments;
            var ret = await InvokeCoreAsync(args ?? [], CancellationToken.None);
            return ret?.ToString() ?? string.Empty;
        }

        public JsonElement GetSchema()
        {
            return tool.JsonSchema;
        }

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            return tool.InvokeAsync(arguments, cancellationToken);
        }
    }
}

public class McpServer(McpServerEntry server)
{
    public McpServerEntry Entry { get; } = server;
    public bool Active { get; set; } = server.Access == AccessState.Autostart;
    public McpClient? McpClient { get; set; }
    public IList<McpClientTool> ClientTools { get; set; } = [];
}
