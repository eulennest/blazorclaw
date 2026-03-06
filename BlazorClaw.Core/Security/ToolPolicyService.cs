using BlazorClaw.Core.Tools;

namespace BlazorClaw.Core.Security;

public interface IToolPolicyProvider
{
    IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, ToolContext context);
    void BeforeTool(ITool tool, string arguments, ToolContext context);
    void AfterTool(ITool tool, string arguments, string result, ToolContext context);
}

public class ToolPolicyAggregator : IToolPolicyProvider
{
    private readonly IEnumerable<IToolPolicyProvider> _providers;

    public ToolPolicyAggregator(IEnumerable<IToolPolicyProvider> providers)
    {
        _providers = providers;
    }

    public IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, ToolContext context)
    {
        var result = allTools;
        foreach (var provider in _providers)
        {
            result = provider.FilterTools(result, context);
        }
        return result;
    }

    public void BeforeTool(ITool tool, string arguments, ToolContext context)
    {
        foreach (var provider in _providers) provider.BeforeTool(tool, arguments, context);
    }

    public void AfterTool(ITool tool, string arguments, string result, ToolContext context)
    {
        foreach (var provider in _providers) provider.AfterTool(tool, arguments, result, context);
    }
}
