using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Web;

namespace BlazorClaw.Server.Components.Account;

internal sealed class IdentityRedirectManager
{
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<IdentityRedirectManager> _logger;

    public IdentityRedirectManager(NavigationManager navigationManager, ILogger<IdentityRedirectManager> logger)
    {
        _navigationManager = navigationManager;
        _logger = logger;
    }

    public void RedirectTo(string uri)
    {
        _logger.LogInformation("Redirecting to {Uri}", uri);
        _navigationManager.NavigateTo(uri, forceLoad: true);
    }

    public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
    {
        var uriWithQuery = _navigationManager.GetUriWithQueryParameters(uri, queryParameters);
        RedirectTo(uriWithQuery);
    }

    public void RedirectToWithStatus(string uri, string message, HttpContext context)
    {
        context.Response.Cookies.Append("Identity.External", message, new CookieOptions
        {
            Path = "/",
            SameSite = SameSiteMode.Lax
        });
        RedirectTo(uri);
    }

    public event EventHandler<RedirectedEventArgs>? Redirected;

    internal void OnRedirected(string message)
    {
        Redirected?.Invoke(this, new RedirectedEventArgs(message));
    }

    internal class RedirectedEventArgs : EventArgs
    {
        public string Message { get; }

        public RedirectedEventArgs(string message)
        {
            Message = message;
        }
    }
}