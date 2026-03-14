using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Security;
using BlazorClaw.Core.Tools;
using BlazorClaw.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Telegram.Bot.Types;

namespace BlazorClaw.Server.Security;

public class SandboxSecurityProvider(IOptionsMonitor<SecurityOptions>? options, ILogger<SandboxSecurityProvider> logger) : IToolPolicyProvider
{

    public async Task<IEnumerable<ITool>> FilterToolsAsync(IEnumerable<ITool> allTools, MessageContext context)
    {
        if (options == null) return allTools;

        var opts = options.CurrentValue;
        var grps = await context.GetUserGroupsAsync();

        if ((grps?.Count ?? 0) == 0)
            grps = [opts.DefaultGroup];

        logger.LogDebug("Filtering tools for user {UserId} with groups: {Groups}",
            context.UserId, string.Join(", ", grps ?? []));

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);

        foreach (var groupName in grps ?? [])
        {
            var groupKey = opts.UserGroups.Keys.FirstOrDefault(k =>
                string.Equals(k, groupName, StringComparison.OrdinalIgnoreCase));

            if (groupKey == null || !opts.UserGroups.TryGetValue(groupKey, out var userGroup))
            {
                continue;
            }

            logger.LogDebug("Processing group {Group} with {Count} patterns",
                groupName, userGroup.AllowedTools.Count);

            foreach (var pattern in userGroup.AllowedTools)
            {
                if (pattern == "*")
                {
                    logger.LogDebug("Wildcard pattern found, allowing all tools");
                    return allTools;
                }

                matcher.AddInclude(pattern);
                logger.LogDebug("Added pattern {Pattern} from group {Group}", pattern, groupName);
            }
        }

        var filtered = allTools.Where(tool => matcher.Match(tool.Name).HasMatches).ToList();

        logger.LogDebug("Filtered {Allowed}/{Total} tools for user {UserId}",
            filtered.Count, allTools.Count(), context.UserId);

        return filtered;
    }

    public async Task BeforeToolAsync(ITool tool, object parameters, MessageContext context)
    {
        var opts = options?.CurrentValue;
        if (opts == null) return;

        var userId = context.UserId ?? "guest";

        logger.LogDebug("BeforeToolAsync: {Tool} for user {UserId}", tool.Name, userId);

        // 1. Rate Limiting prüfen
        var grps = await context.GetUserGroupsAsync();
        if ((grps?.Count ?? 0) == 0)
            grps = [opts.DefaultGroup];

        if (!await CheckRateLimitAsync(userId, tool.Name, grps ?? [], opts))
        {
            logger.LogWarning("Rate limit exceeded for tool {Tool} and user {UserId}", tool.Name, userId);
            throw new RateLimitExceededException($"Rate limit exceeded for tool '{tool.Name}'");
        }

        // 2. Path Validation (nur Policies: DeniedPaths, AllowedPaths, Sandbox)
        if (parameters is IWorkingPaths workingPaths)
        {
            await ValidatePathsAsync(userId, tool.Name, workingPaths, grps ?? [], opts);
        }

        logger.LogDebug("Tool {Tool} authorized for user {UserId}", tool.Name, userId);
    }

    private async Task ValidatePathsAsync(
        string userId,
        string toolName,
        IWorkingPaths workingPaths,
        IList<string> groups,
        SecurityOptions opts)
    {
        var paths = workingPaths.GetPaths().ToArray();
        if (paths.Length == 0) return;

        logger.LogDebug("Validating {Count} paths for tool {Tool} and user {UserId}", paths.Length, toolName, userId);

        // 1. Sandbox Check (User-Group basiert)
        var allowedSandboxPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool sandboxDisabled = false;

        foreach (var groupName in groups)
        {
            var groupKey = opts.UserGroups.Keys.FirstOrDefault(k =>
                string.Equals(k, groupName, StringComparison.OrdinalIgnoreCase));

            if (groupKey == null || !opts.UserGroups.TryGetValue(groupKey, out var userGroup))
                continue;

            if (!userGroup.Sandbox.Enabled)
            {
                sandboxDisabled = true;
                logger.LogDebug("Sandbox disabled for group {Group}", groupName);
                break;
            }

            foreach (var path in userGroup.Sandbox.Paths)
            {
                allowedSandboxPaths.Add(Path.GetFullPath(path));
            }
        }

        // Sandbox-Prüfung (wenn aktiv)
        if (!sandboxDisabled && allowedSandboxPaths.Count != 0)
        {
            foreach (var path in paths)
            {
                var fullPath = Path.GetFullPath(path);
                bool inSandbox = false;

                foreach (var allowedPath in allowedSandboxPaths)
                {
                    if (fullPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        inSandbox = true;
                        break;
                    }
                }

                if (!inSandbox)
                {
                    logger.LogWarning("Path {Path} outside sandbox for user {UserId}", fullPath, userId);
                    throw new UnauthorizedAccessException($"Access to path '{path}' denied (sandbox violation)");
                }
            }

            logger.LogDebug("Sandbox check passed for user {UserId}", userId);
        }

        // 2. Tool-spezifische Policy-Checks
        if (!opts.ToolPolicies.TryGetValue(toolName, out var policy))
        {
            logger.LogDebug("No tool policy found for {Tool}", toolName);
            return;
        }

        // 2a. DeniedPaths (höchste Priorität)
        if (policy.DeniedPaths.Count != 0)
        {
            var deniedMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in policy.DeniedPaths)
            {
                deniedMatcher.AddInclude(pattern);
            }

            foreach (var path in paths)
            {
                var fullPath = Path.GetFullPath(path);

                if (deniedMatcher.Match(fullPath).HasMatches)
                {
                    logger.LogWarning("Path {Path} denied by tool policy for {Tool}", fullPath, toolName);
                    throw new UnauthorizedAccessException($"Access to path '{path}' denied by policy");
                }
            }

            logger.LogDebug("DeniedPaths check passed for {Tool}", toolName);
        }

        // 2b. AllowedPaths (falls vorhanden)
        if (policy.AllowedPaths.Count != 0)
        {
            var allowedMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in policy.AllowedPaths)
            {
                allowedMatcher.AddInclude(pattern);
            }

            foreach (var path in paths)
            {
                var fullPath = Path.GetFullPath(path);

                if (!allowedMatcher.Match(fullPath).HasMatches)
                {
                    logger.LogWarning("Path {Path} not in allowed paths for {Tool}", fullPath, toolName);
                    throw new UnauthorizedAccessException($"Access to path '{path}' not allowed by tool policy");
                }
            }

            logger.LogDebug("AllowedPaths check passed for {Tool}", toolName);
        }

        logger.LogDebug("Path validation passed for tool {Tool}", toolName);
    }

    private static async Task<bool> CheckRateLimitAsync(
        string userId,
        string toolName,
        IList<string> groups,
        SecurityOptions opts)
    {
        // TODO: Implementiere Rate Limit Check mit RateLimitTracking DB
        return true;
    }

    public async Task<string> AfterToolAsync(ITool tool, object parameters, string result, MessageContext context)
    {
        var opts = options?.CurrentValue;
        if (opts == null) return result;

        var userId = context.UserId ?? "guest";
        var success = !string.IsNullOrEmpty(result) && !result.Contains("error", StringComparison.OrdinalIgnoreCase);

        logger.LogDebug("AfterToolAsync: {Tool} for user {UserId}, Success: {Success}", tool.Name, userId, success);

        try
        {
            // 1. Rate Limit Tracking speichern
            await SaveRateLimitTrackingAsync(context, tool.Name, 0, success);

            // 2. Audit Log (falls aktiviert)
            if (opts.ToolPolicies.TryGetValue(tool.Name, out var policy) && policy.AuditLog)
            {
                await SaveAuditLogAsync(context, tool.Name, parameters, result);
            }

            // 3. Notification (falls aktiviert und kritisch)
            if (opts.ToolPolicies.TryGetValue(tool.Name, out policy) && policy.NotifyUser)
            {
                await SendNotificationAsync(userId, tool.Name, parameters, result);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AfterToolAsync for tool {Tool}", tool.Name);
        }

        return result;
    }

    private async Task SaveRateLimitTrackingAsync(MessageContext context, string toolName, int tokenCount, bool success)
    {
        try
        {
            using var dbContext = context.Provider.GetRequiredService<ApplicationDbContext>();
            var userId = context.UserId ?? context.Channel?.ToString() ?? string.Empty;
            var opts = options?.CurrentValue;
            if (opts == null) return;

            // LimitKeys sammeln (GroupName + ToolName)
            var limitKeys = new HashSet<string> { toolName };

            if (opts.ToolPolicies.TryGetValue(toolName, out var policy))
            {
                foreach (var rateLimit in policy.RateLimits)
                {
                    if (!string.IsNullOrEmpty(rateLimit.GroupName))
                    {
                        limitKeys.Add(rateLimit.GroupName);
                    }
                }
            }

            // Tracking für alle Keys erstellen
            foreach (var limitKey in limitKeys)
            {
                dbContext.RateLimitTrackings.Add(new RateLimitTracking
                {
                    UserId = userId,
                    ToolName = toolName,
                    LimitKey = limitKey,
                    TokenCount = tokenCount,
                    Success = success,
                    Timestamp = DateTime.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();

            logger.LogDebug("Rate limit tracking saved for user {UserId}, tool {Tool}, keys: {Keys}",
                userId, toolName, string.Join(", ", limitKeys));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save rate limit tracking for tool {Tool}", toolName);
        }
    }

    private async Task SaveAuditLogAsync(MessageContext context, string toolName, object parameters, string result)
    {
        try
        {
            using var dbContext = context.Provider.GetRequiredService<ApplicationDbContext>();
            var userId = context.UserId ?? context.Channel?.ToString() ?? string.Empty;
            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = toolName,
                Details = JsonSerializer.Serialize(parameters),
                Result = result?.Length > 1000 ? result[..1000] + "..." : result,
                SessionId = context.Session!.Id,
                Timestamp = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();

            logger.LogDebug("Audit log saved for user {UserId}, action {Action}", userId, toolName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save audit log for tool {Tool}", toolName);
        }
    }

    private async Task SendNotificationAsync(string userId, string toolName, object parameters, string result)
    {
        try
        {
            // TODO: Implementiere Notification Service (Email, Telegram, etc.)
            logger.LogWarning("Critical action executed: Tool {Tool} by user {UserId}", toolName, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification for tool {Tool}", toolName);
        }
    }
}
public class RateLimitExceededException(string message) : Exception(message)
{
}