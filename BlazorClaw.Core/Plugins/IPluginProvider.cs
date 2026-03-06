using Microsoft.Extensions.DependencyInjection;

namespace BlazorClaw.Core.Plugins;

public interface IPluginProvider
{
    void ConfigureServices(IServiceCollection services);
}
