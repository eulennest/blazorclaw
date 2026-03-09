using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Tools;

namespace BlazorClaw.Core.Security;

public interface IMessagePolicyProvider
{
    string FilterUserMessage(string message, MessageContext context);
    string FilterAssistantMessage(string message, MessageContext context);
}

public class MessagePolicyAggregator(IEnumerable<IMessagePolicyProvider> providers) : IMessagePolicyProvider
{
    public string FilterUserMessage(string message, MessageContext context)
    {
        var result = message;
        foreach (var provider in providers)
        {
            result = provider.FilterUserMessage(result, context);
        }
        return result;
    }

    public string FilterAssistantMessage(string message, MessageContext context)
    {
        var result = message;
        foreach (var provider in providers)
        {
            result = provider.FilterAssistantMessage(result, context);
        }
        return result;
    }
}
