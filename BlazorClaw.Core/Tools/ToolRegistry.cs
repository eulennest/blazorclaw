using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Plugins;
using Microsoft.Extensions.AI;

namespace BlazorClaw.Core.Tools;

public class ToolRegistry : IToolProvider
{
    private readonly Dictionary<string, ITool> _tools = new();

    public ToolRegistry(IServiceProvider sp)
    {
        var tools = PluginUtils.BuildPlugins<ITool>(sp);
        foreach (var tool in tools)
        {
            _tools[tool.Name] = tool;
        }
    }

    public IAsyncEnumerable<ITool> GetAllToolsAsync() => _tools.Values.ToAsyncEnumerable();

    public ITool? GetTool(string name) => _tools.GetValueOrDefault(name);
}

public static class ToolExtensions
{
    public static AITool AsAiTool(this ITool tool, MessageContext context)
    {
        if (tool is AITool ait) return ait;
        return new AIToolWrapper(tool, context);
    }
}
