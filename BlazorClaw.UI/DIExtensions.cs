using Microsoft.Extensions.DependencyInjection;

namespace BlazorClaw.UI
{
    public static class DIExtensions
    {
        public static IServiceCollection AddClawUI(this IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddRazorComponents()
                .AddInteractiveServerComponents();
            return services;
        }
    }
}
