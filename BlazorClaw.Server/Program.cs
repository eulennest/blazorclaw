using BlazorClaw.Server.Components;
using BlazorClaw.Server.Components.Account;
using BlazorClaw.Server.Data;
using BlazorClaw.Server.Security;
using BlazorClaw.Server.Memory;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Plugins;
using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Server.Security.Vault;
using BlazorClaw.Server.Web;
using BlazorClaw.Core.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;
using BlazorClaw.Core.Providers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BlazorClaw.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddHttpClient("OpenRouter", client =>
{
    var llmConfig = builder.Configuration.GetSection("Llm");
    client.BaseAddress = new Uri(llmConfig["BaseUrl"] ?? "https://openrouter.ai/api/v1/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {llmConfig["ApiKey"]}");
});

// Add HttpClient for WebSearchProvider
builder.Services.AddHttpClient<IWebSearchProvider, BraveSearchProvider>();
builder.Services.Configure<WebSearchOptions>(builder.Configuration.GetSection(WebSearchOptions.Section));

// Register Plugin Services
var plugins = PluginUtils.BuildPlugins<IPluginProvider>(builder.Services.BuildServiceProvider());
foreach (var plugin in plugins)
{
    plugin.ConfigureServices(builder.Services);
}

builder.Services.TryAddSingleton<IVaultProvider, JsonVaultProvider>();
builder.Services.TryAddSingleton<ISessionManager, SessionManager>();

// Tool registry
builder.Services.AddSingleton<IToolRegistry>(sp => new ToolRegistry(PluginUtils.BuildPlugins<ITool>(sp)));

// Security
builder.Services.TryAddSingleton<IToolPolicyProvider>(sp => new ToolPolicyAggregator(PluginUtils.BuildPlugins<IToolPolicyProvider>(sp, typeof(ToolPolicyAggregator))));
builder.Services.TryAddSingleton<IMessagePolicyProvider>(sp => new MessagePolicyAggregator(PluginUtils.BuildPlugins<IMessagePolicyProvider>(sp, typeof(MessagePolicyAggregator))));

// Memory Search
builder.Services.Configure<FileSystemMemoryOptions>(builder.Configuration.GetSection(FileSystemMemoryOptions.Section));
builder.Services.TryAddSingleton<IMemorySearchProvider>(sp => new MemorySearchAggregator(PluginUtils.BuildPlugins<IMemorySearchProvider>(sp, typeof(MemorySearchAggregator))));

// Providers
builder.Services.TryAddSingleton<IProviderManager>(sp => new ProviderAggregator(PluginUtils.BuildPlugins<IProviderManager>(sp, typeof(ProviderAggregator))));


// Add SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));


// Add IdentityRedirectManager
builder.Services.TryAddScoped<BlazorClaw.Server.Components.Account.IdentityRedirectManager>();


builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<IdentityUserAccessor>();

builder.Services.AddScoped<IdentityRedirectManager>();

builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddSingleton<BlazorClaw.Server.Services.ISessionManager, BlazorClaw.Server.Services.SessionManager>();
builder.Services.AddScoped<BlazorClaw.Server.Services.IChannelToSessionDispatcher, BlazorClaw.Server.Services.ChannelToSessionDispatcher>();

builder.Services.AddHostedService<BlazorClaw.Channels.Services.TelegramBotHostedService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse("192.168.0.0/16"));
    options.ForwardLimit = 2;
});


var app = builder.Build();

// Initialize database
await DbInitializer.InitializeAsync(app.Services, app.Configuration);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>();
app.MapControllers();

app.Run();
