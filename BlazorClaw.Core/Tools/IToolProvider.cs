namespace BlazorClaw.Core.Tools;

public interface IToolProvider
{
    IAsyncEnumerable<ITool> GetAllToolsAsync();
    ITool? GetTool(string name);
}
