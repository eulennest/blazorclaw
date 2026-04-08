using BlazorClaw.Core.Plugins;

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
