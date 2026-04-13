using BlazorClaw.Channels.Services;
using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Plugins;
using BlazorClaw.Core.Providers;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Security.Vault;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Speech;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using BlazorClaw.Core.VFS;
using BlazorClaw.Core.Web;
using BlazorClaw.Server;
using BlazorClaw.Server.Security.Vault;
using BlazorClaw.Server.Services;
using BlazorClaw.Server.Tools;
using BlazorClaw.Server.Tools.Mcp;
using BlazorClaw.Server.Web;
using BlazorClaw.UI;
using BlazorClaw.UI.Components.Account;
using Matrix.Sdk;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

var confFile = $"appsettings.{builder.Environment.EnvironmentName}.json";
var old = builder.Configuration.Sources.LastOrDefault(o => o is FileConfigurationSource jc && (jc.Path?.Contains(confFile) ?? false));
if (old != null) builder.Configuration.Sources.Remove(old);
builder.Configuration.Add<SaveableJsonConfigurationSource>(o =>
{
    o.ReloadOnChange = true;
    o.Optional = true;
    o.Path = confFile;
});
builder.Services.AddSingleton<IConfigurationRoot>(builder.Configuration);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Add HttpClient for WebSearchProvider
builder.Services.AddHttpClient<IWebSearchProvider, BraveSearchProvider>();
builder.Services.AddHttpClient<BlazorClaw.Server.Tools.ImageGenerationTool>();

// Add HttpClients for HttpRequestTool (normal + insecure for self-signed certs)
builder.Services.AddHttpClient("HttpClient")
    .ConfigureHttpClient(client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Compatible; BlazorClaw/1.0)");
    });
builder.Services.AddHttpClient("InsecureHttpClient")
    .ConfigureHttpClient(client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Compatible; BlazorClaw/1.0)");
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        return handler;
    });
builder.Services.Configure<WebSearchOptions>(builder.Configuration.GetSection(WebSearchOptions.Section));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.Section));
builder.Services.AddSingleton<PathHelper>();

// Register Plugin Services
var plugins = PluginUtils.BuildPlugins<IPluginProvider>(builder.Services.BuildServiceProvider());
foreach (var plugin in plugins)
{
    plugin.ConfigureServices(builder.Services);
}
builder.Services.TryAddSingleton<ISessionManager, SessionManager>();

// Variable Resolution Service
builder.Services.TryAddScoped<IVariableResolver, VariableResolver>();
builder.Services.TryAddScoped<VariableResolverHelper>();

builder.Services.Configure<JsonVaultOptions>(builder.Configuration.GetSection(JsonVaultOptions.Section));
builder.Services.Configure<BitwardenOptions>(builder.Configuration.GetSection(BitwardenOptions.Section));
builder.Services.TryAddScoped<JsonVaultProvider>();
builder.Services.TryAddScoped<BitwardenVaultProvider>();
builder.Services.TryAddScoped<DbUserApiKeyVaultProvider>();
builder.Services.TryAddScoped<DbReadonlyApiKeyVaultProvider>();
builder.Services.AddScoped<VaultProviderInfo>(sp => new VaultProviderInfo
{
    Id = "json-main",
    Type = "json",
    Title = "JSON Vault",
    Description = "Lokaler verschlüsselter JSON-Vault",
    CanWrite = true,
    Provider = sp.GetRequiredService<JsonVaultProvider>()
});
builder.Services.AddScoped<VaultProviderInfo>(sp => new VaultProviderInfo
{
    Id = "db-userkeys",
    Type = "dbkeys",
    Title = "DB User Keys",
    Description = "Normale benutzerbezogene API-Keys aus der Datenbank (schreibbar)",
    CanWrite = true,
    Provider = sp.GetRequiredService<DbUserApiKeyVaultProvider>()
});
builder.Services.AddScoped<VaultProviderInfo>(sp => new VaultProviderInfo
{
    Id = "db-readonly",
    Type = "dbkeys",
    Title = "DB Readonly Keys",
    Description = "System- und OAuth-Keys aus der Datenbank (read-only)",
    CanWrite = false,
    Provider = sp.GetRequiredService<DbReadonlyApiKeyVaultProvider>()
});
builder.Services.TryAddScoped<IVaultManager, VaultManager>();

builder.Services.TryAddScoped<SessionStateAccessor>();
builder.Services.TryAddScoped<MessageContextAccessor>();
builder.Services.TryAddScoped<IVfsSystem>(sp => PathUtils.BuildVFSAsync(sp).GetAwaiter().GetResult());
// Tool registry
builder.Services.TryAddScoped<McpToolRegistry>();
builder.Services.TryAddScoped<IToolProvider>(sp =>
{
    var pl = PluginUtils.BuildPlugins<IToolProvider>(sp, typeof(ToolAggregator), typeof(McpToolRegistry));
    var mcp = sp.GetRequiredService<McpToolRegistry>();
    return new ToolAggregator(pl.Concat([mcp]));
});

// Security
builder.Services.TryAddScoped<IToolPolicyProvider>(sp => new ToolPolicyAggregator(PluginUtils.BuildPlugins<IToolPolicyProvider>(sp, typeof(ToolPolicyAggregator))));
builder.Services.TryAddScoped<IMessagePolicyProvider>(sp => new MessagePolicyAggregator(PluginUtils.BuildPlugins<IMessagePolicyProvider>(sp, typeof(MessagePolicyAggregator))));

// Memory Search
builder.Services.TryAddScoped<IMemorySearchProvider>(sp => new MemorySearchAggregator(PluginUtils.BuildPlugins<IMemorySearchProvider>(sp, typeof(MemorySearchAggregator))));

// Commands
builder.Services.TryAddScoped<ICommandProvider>(sp => new SystemCommandAggregator(PluginUtils.BuildPlugins<ICommandProvider>(sp, typeof(SystemCommandAggregator))));

// Providers
builder.Services.TryAddScoped<IProviderManager>(sp => new ProviderAggregator(PluginUtils.BuildPlugins<IProviderManager>(sp, typeof(ProviderAggregator))));

builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.Section));

// Add SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Transient);


// Add IdentityRedirectManager
builder.Services.TryAddScoped<IdentityRedirectManager>();

builder.Services.AddCascadingAuthenticationState();

builder.Services.TryAddScoped<IdentityUserAccessor>();
builder.Services.TryAddScoped<ITextToSpeechProvider, OpenAiTtsProvider>();
builder.Services.TryAddScoped<ISpeechToTextProvider, OpenAiTtsProvider>();

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

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityEmailSender>();

builder.Services.AddSingleton<ISessionManager, BlazorClaw.Server.Services.SessionManager>();
builder.Services.AddSingleton<IMessageDispatcher, MessageDispatcher>();
builder.Services.AddScoped<ISessionQueryService, BlazorClaw.Core.Services.SessionQueryService>();

builder.Services.AddSingleton<MatrixClientFactory>();
builder.Services.AddSingleton<ChannelRegistry>();
builder.Services.AddSingleton<CronJobService>();
builder.Services.AddSingleton<ICronJobService>(sp => sp.GetRequiredService<CronJobService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CronJobService>());

builder.Services.Configure<BotConfigs<WhatsAppBotEntry>>(builder.Configuration.GetSection(BotConfigs<WhatsAppBotEntry>.Section));
builder.Services.Configure<BotConfigs<TelegramBotEntry>>(builder.Configuration.GetSection(BotConfigs<TelegramBotEntry>.Section));
builder.Services.Configure<BotConfigs<MatrixBotEntry>>(builder.Configuration.GetSection(BotConfigs<MatrixBotEntry>.Section));

builder.Services.AddHostedService<BotHostedService<MatrixBotEntry, MatrixChannelBot>>();
builder.Services.AddHostedService<BotHostedService<TelegramBotEntry, TelegramChannelBot>>();
builder.Services.AddHostedService<BotHostedService<WhatsAppBotEntry, WhatsAppChannelBot>>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse("192.168.0.0/16"));
    options.ForwardLimit = 2;
});

builder.Services.AddClawUI();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
          ["application/octet-stream"]);
});

var app = builder.Build();

// Initialize database
DbInitializer.EnsureMasterKey(app.Configuration);
await DbInitializer.InitializeAsync(app.Services, app.Configuration);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseResponseCompression();
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<BlazorClaw.UI.Components.App>()
    .AddInteractiveServerRenderMode();
app.MapControllers();
app.MapHub<BlazorClaw.Server.Hubs.ChatHub>("/chatHub");

app.Run();
