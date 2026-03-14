using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Core.Security;

public interface IToolPolicyProvider
{
    Task<IEnumerable<ITool>> FilterToolsAsync(IEnumerable<ITool> allTools, MessageContext context);
    Task BeforeToolAsync(ITool tool, object parameters, MessageContext context);
    Task<string> AfterToolAsync(ITool tool, object parameters, string result, MessageContext context);
}

public class ToolPolicyAggregator(IEnumerable<IToolPolicyProvider> providers) : IToolPolicyProvider
{
    public async Task<IEnumerable<ITool>> FilterToolsAsync(IEnumerable<ITool> allTools, MessageContext context)
    {
        var result = allTools;
        foreach (var provider in providers)
        {
            result = await provider.FilterToolsAsync(result, context);
        }
        return result;
    }

    public async Task BeforeToolAsync(ITool tool, object parameters, MessageContext context)
    {
        foreach (var provider in providers) await provider.BeforeToolAsync(tool, parameters, context);
    }

    public async Task<string> AfterToolAsync(ITool tool, object parameters, string result, MessageContext context)
    {
        var finalResult = result;
        foreach (var provider in providers)
        {
            finalResult = await provider.AfterToolAsync(tool, parameters, finalResult, context);
        }
        return finalResult;
    }
}
