using BlazorClaw.Core.Tools;

namespace BlazorClaw.Core.Security;

public interface IMessagePolicyProvider
{
    string FilterUserMessage(string message, ToolContext context);
    string FilterAssistantMessage(string message, ToolContext context);
}

public class MessagePolicyAggregator : IMessagePolicyProvider
{
    private readonly IEnumerable<IMessagePolicyProvider> _providers;

    public MessagePolicyAggregator(IEnumerable<IMessagePolicyProvider> providers)
    {
        _providers = providers;
    }

    public string FilterUserMessage(string message, ToolContext context)
    {
        var result = message;
        foreach (var provider in _providers)
        {
            result = provider.FilterUserMessage(result, context);
        }
        return result;
    }

    public string FilterAssistantMessage(string message, ToolContext context)
    {
        var result = message;
        foreach (var provider in _providers)
        {
            result = provider.FilterAssistantMessage(result, context);
        }
        return result;
    }
}
