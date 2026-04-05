namespace Baileys.Utils;

/// <summary>
/// Minimal structured logger interface, mirroring the TypeScript
/// <c>Utils/logger.ts</c> ILogger interface.
///
/// Any standard logger (Microsoft.Extensions.Logging, Serilog, NLog, etc.)
/// can be wrapped to implement this interface.
/// </summary>
public interface ILogger
{
    /// <summary>The minimum level at which messages are recorded.</summary>
    string Level { get; }

    /// <summary>Creates a child logger that adds <paramref name="context"/> to every entry.</summary>
    ILogger Child(IReadOnlyDictionary<string, object> context);

    void Trace(object message, string? template = null);
    void Debug(object message, string? template = null);
    void Info(object message, string? template = null);
    void Warn(object message, string? template = null);
    void Error(object message, string? template = null);
    void Exception(Exception ex);
}

/// <summary>
/// A no-op logger implementation — useful as a default / in tests.
/// </summary>
public sealed class NullLogger : ILogger
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly NullLogger Instance = new();

    public string Level => "silent";

    public ILogger Child(IReadOnlyDictionary<string, object> _) => this;

    public void Trace(object _, string? __ = null) { }
    public void Debug(object _, string? __ = null) { }
    public void Info(object _, string? __ = null) { }
    public void Warn(object _, string? __ = null) { }
    public void Error(object _, string? __ = null) { }

    public void Exception(Exception ex) {  }
}

/// <summary>
/// A console logger that mirrors the pino-style output used by the TypeScript
/// Baileys package (timestamp + level + message).
/// </summary>
public sealed class ConsoleLogger : ILogger
{
    private readonly string _level;
    private readonly IReadOnlyDictionary<string, object> _context;

    private static readonly string[] Levels = ["trace", "debug", "info", "warn", "error", "silent"];

    public ConsoleLogger(string level = "info", IReadOnlyDictionary<string, object>? context = null)
    {
        _level = level.ToLowerInvariant();
        _context = context ?? new Dictionary<string, object>();
    }

    public string Level => _level;

    public ILogger Child(IReadOnlyDictionary<string, object> context)
    {
        var merged = new Dictionary<string, object>(_context);
        foreach (var (k, v) in context) merged[k] = v;
        return new ConsoleLogger(_level, merged);
    }

    public void Trace(object msg, string? t = null) => Log("trace", msg, t);
    public void Debug(object msg, string? t = null) => Log("debug", msg, t);
    public void Info(object msg, string? t = null)  => Log("info",  msg, t);
    public void Warn(object msg, string? t = null)  => Log("warn",  msg, t);
    public void Error(object msg, string? t = null) => Log("error", msg, t);

    private void Log(string level, object message, string? template)
    {
        if (Array.IndexOf(Levels, level) < Array.IndexOf(Levels, _level)) return;

        var ctx = _context.Count > 0
            ? " " + string.Join(" ", _context.Select(kv => $"{kv.Key}={kv.Value}"))
            : string.Empty;

        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] {level.ToUpperInvariant()}{ctx}: {template ?? message}");
    }

    public void Exception(Exception ex)
    {
        Log("error", ex.ToString(), null);
    }
}
