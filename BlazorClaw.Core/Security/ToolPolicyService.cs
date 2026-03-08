using BlazorClaw.Core.Tools;

namespace BlazorClaw.Core.Security;

public interface IToolPolicyProvider
{
    IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, ToolContext context);
    void BeforeTool(ITool tool, object parameters, ToolContext context);
    string AfterTool(ITool tool, object parameters, string result, ToolContext context);
}

public class ToolPolicyAggregator(IEnumerable<IToolPolicyProvider> providers) : IToolPolicyProvider
{
    public IEnumerable<ITool> FilterTools(IEnumerable<ITool> allTools, ToolContext context)
    {
        var result = allTools;
        foreach (var provider in providers)
        {
            result = provider.FilterTools(result, context);
        }
        return result;
    }

    public void BeforeTool(ITool tool, object parameters, ToolContext context)
    {
        foreach (var provider in providers) provider.BeforeTool(tool, parameters, context);
    }

    public string AfterTool(ITool tool, object parameters, string result, ToolContext context)
    {
        var finalResult = result;
        foreach (var provider in providers)
        {
            finalResult = provider.AfterTool(tool, parameters, finalResult, context);
        }
        return finalResult;
    }
}
