using BlazorClaw.Server.Components;
using BlazorClaw.Server.Components.Account;
using BlazorClaw.Server.Data;
using BlazorClaw.Server.Security;
using BlazorClaw.Server.Security.Vault;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Memory;
using BlazorClaw.Core.Plugins;
using BlazorClaw.Core.Security.Vault;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

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
builder.Services.AddHttpClient<BlazorClaw.Core.Web.IWebSearchProvider, BlazorClaw.Server.Web.BraveSearchProvider>();
builder.Services.Configure<BlazorClaw.Server.Web.WebSearchOptions>(builder.Configuration.GetSection(BlazorClaw.Server.Web.WebSearchOptions.Section));

// Tool registry & Security
builder.Services.AddSingleton<IToolRegistry>(sp => new ToolRegistry(PluginUtils.BuildPlugins<ITool>(sp)));
builder.Services.AddScoped<IToolPolicyProvider>(sp => new ToolPolicyAggregator(PluginUtils.BuildPlugins<IToolPolicyProvider>(sp, typeof(ToolPolicyAggregator))));
builder.Services.AddScoped<IMessagePolicyProvider>(sp => new MessagePolicyAggregator(PluginUtils.BuildPlugins<IMessagePolicyProvider>(sp, typeof(MessagePolicyAggregator))));
builder.Services.AddScoped<IVaultProvider, JsonVaultProvider>();
builder.Services.AddSingleton<IMemorySearchProvider>(new BlazorClaw.Server.Memory.FileSystemMemorySearchProvider("./memory"));

// Add SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));


// Add IdentityRedirectManager
builder.Services.AddScoped<BlazorClaw.Server.Components.Account.IdentityRedirectManager>();


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
