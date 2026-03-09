namespace BlazorClaw.Core.Security;

public interface IWorkingPaths
{
    IEnumerable<string> GetAllowedPaths();
}
