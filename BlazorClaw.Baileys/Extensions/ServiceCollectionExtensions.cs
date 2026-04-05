using Baileys.Options;
using Baileys.Session;
using Baileys.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Baileys.Extensions;

/// <summary>
/// Extension methods for registering Baileys services in an
/// <see cref="IServiceCollection"/> (Microsoft.Extensions.DependencyInjection).
/// </summary>
/// <example>
/// <para><b>Minimal — in-memory session (no persistence):</b></para>
/// <code>
/// builder.Services.AddBaileys(o =>
/// {
///     o.PhoneNumber = "15551234567";
/// });
/// </code>
///
/// <para><b>File-based session persistence:</b></para>
/// <code>
/// builder.Services.AddBaileysWithFileStorage(
///     filePath: "baileys_auth.json",
///     configure: o => { o.PhoneNumber = "15551234567"; });
/// </code>
///
/// <para><b>Custom session provider (e.g. database-backed):</b></para>
/// <code>
/// builder.Services.AddBaileysWithProvider&lt;MyDatabaseAuthStateProvider&gt;(
///     o => { o.PhoneNumber = "15551234567"; });
/// </code>
///
/// <para><b>Bind options from <c>appsettings.json</c> ("Baileys" section):</b></para>
/// <code>
/// // In appsettings.json:
/// // { "Baileys": { "PhoneNumber": "15551234567", "UnarchiveChats": false } }
/// builder.Services.Configure&lt;BaileysOptions&gt;(
///     builder.Configuration.GetSection(BaileysOptions.SectionName));
/// builder.Services.AddBaileys();
/// </code>
/// </example>
public static class ServiceCollectionExtensions
{
	// ─────────────────────────────────────────────────────────────────────────
	//  AddBaileys — in-memory provider (default)
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Registers Baileys with an <see cref="InMemoryAuthStateProvider"/> and optional
	/// programmatic configuration of <see cref="BaileysOptions"/>.
	/// Session state is kept in process memory and lost on restart.
	/// </summary>
	public static IServiceCollection AddBaileys(
		this IServiceCollection services,
		Action<BaileysOptions>? configure = null
	)
	{
		AddOptions(services, configure);
		AddBaileysCore(services);
		services.AddSingleton<IAuthStateProvider, InMemoryAuthStateProvider>();
		return services;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  AddBaileysWithFileStorage — file-based provider
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Registers Baileys with a <see cref="FileAuthStateProvider"/> that persists
	/// credentials to <paramref name="filePath"/> as JSON.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="filePath">
	/// Path to the credentials JSON file (e.g. <c>"baileys_auth.json"</c> or an
	/// absolute path).  The directory must already exist; the file is created on
	/// first save.
	/// </param>
	/// <param name="configure">Optional delegate to configure <see cref="BaileysOptions"/>.</param>
	public static IServiceCollection AddBaileysWithFileStorage(
		this IServiceCollection services,
		string filePath,
		Action<BaileysOptions>? configure = null
	)
	{
		AddOptions(services, configure);
		AddBaileysCore(services);
		services.AddSingleton<IAuthStateProvider>(_ => new FileAuthStateProvider(filePath));
		return services;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  AddBaileysWithDirectoryStorage — directory-based provider
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Registers Baileys with a <see cref="DirectoryAuthStateProvider"/> that
	/// persists both credentials and Signal-protocol keys under
	/// <paramref name="directory"/> — the direct .NET equivalent of the
	/// TypeScript <c>useMultiFileAuthState(folder)</c> helper.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="directory">
	/// Path to the directory where session state is stored (e.g.
	/// <c>"baileys_auth_info"</c> or an absolute path).  The directory is
	/// created automatically if it does not exist.
	/// </param>
	/// <param name="configure">Optional delegate to configure <see cref="BaileysOptions"/>.</param>
	/// <example>
	/// <code>
	/// builder.Services.AddBaileysWithDirectoryStorage(
	///     directory: "baileys_auth_info",
	///     configure: o => { o.PhoneNumber = "15551234567"; });
	/// </code>
	/// </example>
	public static IServiceCollection AddBaileysWithDirectoryStorage(
		this IServiceCollection services,
		string directory,
		Action<BaileysOptions>? configure = null
	)
	{
		AddOptions(services, configure);
		AddBaileysCore(services);
		services.AddSingleton<IAuthStateProvider>(_ => new DirectoryAuthStateProvider(directory));
		return services;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  AddBaileysWithProvider<T> — custom provider
	// ─────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Registers Baileys with a custom <typeparamref name="TProvider"/> as the
	/// <see cref="IAuthStateProvider"/> implementation.
	/// Use this to plug in a database-backed provider (e.g. Entity Framework,
	/// Redis, Cosmos DB, etc.).
	/// </summary>
	/// <typeparam name="TProvider">
	/// Concrete implementation of <see cref="IAuthStateProvider"/>.
	/// Registered as a <em>singleton</em>.
	/// </typeparam>
	public static IServiceCollection AddBaileysWithProvider<TProvider>(
		this IServiceCollection services,
		Action<BaileysOptions>? configure = null
	)
		where TProvider : class, IAuthStateProvider
	{
		AddOptions(services, configure);
		AddBaileysCore(services);
		services.AddSingleton<IAuthStateProvider, TProvider>();
		return services;
	}

	// ─────────────────────────────────────────────────────────────────────────
	//  Private helpers
	// ─────────────────────────────────────────────────────────────────────────

	private static void AddOptions(IServiceCollection services, Action<BaileysOptions>? configure)
	{
		var builder = services.AddOptions<BaileysOptions>();
		if (configure is not null)
			builder.Configure(configure);
	}

	private static void AddBaileysCore(IServiceCollection services)
	{
		services.AddSingleton<IBaileysEventEmitter, BaileysEventEmitter>();
		services.AddSingleton<BaileysClient>();
		services.AddSingleton<ILogger>(_ => new ConsoleLogger());
		services.AddHostedService<BaileysClientHostedService>();
	}
}
