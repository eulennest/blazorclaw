using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Plugins;
using Microsoft.Extensions.AI;
using System.Text.Json;

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
    public static AITool AsAiTool(this ITool tool, MessageContext context) => new AIToolWrapper(tool, context);
}
public class AIToolWrapper(ITool tool, MessageContext context) : AIFunction
{
    private readonly ITool _tool = tool;

    public override string Name => _tool.Name;
    public override string Description => _tool.Description;

    public override JsonElement JsonSchema => _tool.GetSchema();

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await _tool.ExecuteAsync(arguments.Context!, context).ConfigureAwait(false);
    }
}
