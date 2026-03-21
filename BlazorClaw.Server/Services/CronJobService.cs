using BlazorClaw.Core.Commands;
using BlazorClaw.Core.Data;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Sessions;
using BlazorClaw.Core.Utils;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazorClaw.Server.Services
{
    public class CronJobService(IServiceScopeFactory scopeFactory, ILogger<CronJobService> logger) : BackgroundService, ICronJobService
    {
        CancellationTokenSource cancellationTokenSource = new();
        public void ForceExecute()
        {
            cancellationTokenSource.Cancel();
        }

        TimeSpan MaxDelay = TimeSpan.FromHours(1);
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            logger.LogInformation("CronJobService started");
            var aktDelay = TimeSpan.FromMinutes(1);
            while (!ct.IsCancellationRequested)
            {
                if (cancellationTokenSource.IsCancellationRequested) cancellationTokenSource = new();
                await Task.Delay(aktDelay, cancellationTokenSource.Token).NoThrow();
                if (ct.IsCancellationRequested) break;

                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var start = DateTime.UtcNow;
                    var list = await db.Crontabs.AsNoTracking().Where(o => o.NextExecution == null || o.NextExecution <= DateTime.UtcNow).ToListAsync(cancellationToken: ct);

                    if (list.Count > 0)
                    {
                        logger.LogInformation("Found {CronJobCount} cron job(s) to execute", list.Count);
                    }
                    else
                    {
                        logger.LogDebug("No cron jobs due for execution");
                    }

                    foreach (var job in list)
                    {
                        db.ChangeTracker.Clear();
                        db.Crontabs.Update(job);
                        await InternalHandleJobAsync(job, db);
                    }

                    var nexteexc = await db.Crontabs.AnyAsync(cancellationToken: ct)
                        ? (await db.Crontabs.MinAsync(o => o.NextExecution, cancellationToken: ct)) ?? DateTime.MaxValue
                        : DateTime.MaxValue;

                    var newDelay = TimeSpan.FromSeconds(Math.Min(MaxDelay.TotalSeconds, ((nexteexc - start) + TimeSpan.FromSeconds(10)).TotalSeconds));
                    if (newDelay != aktDelay)
                    {
                        logger.LogDebug("Adjusted cron loop delay to {DelaySeconds}s (next execution: {NextExecution})", newDelay.TotalSeconds, nexteexc);
                        aktDelay = newDelay;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in cron job execution loop");
                }
            }
            logger.LogInformation("CronJobService stopped");
        }

        private async Task InternalHandleJobAsync(Crontab job, ApplicationDbContext db)
        {
            try
            {
                var cron = Cronos.CronExpression.Parse(job.Cron);
                job.LastExecution = DateTime.UtcNow;
                job.NextExecution = cron.GetNextOccurrence(DateTime.UtcNow);

                logger.LogDebug("Executing cron job {CronJobId} ({Description}), next execution: {NextExecution}",
                    job.Id, job.Description, job.NextExecution);
                await ExecuteJobAsync(job);
                logger.LogInformation("Cron job {CronJobId} executed successfully", job.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cron job {CronJobId} ({Description}) failed", job.Id, job.Description);
                var al = new AuditLog()
                {
                    Action = "cron",
                    Details = System.Text.Json.JsonSerializer.Serialize(job),
                    Result = ex.ToString(),
                    Timestamp = DateTime.UtcNow,
                    SessionId = job.SessionId
                };
                db.AuditLogs.Add(al);
            }
            await db.SaveChangesAsync();
        }


        public async Task ExecuteNow(Guid cronJobId)
        {
            logger.LogInformation("Manual execution requested for cron job {CronJobId}", cronJobId);
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var job = await db.Crontabs.FindAsync(cronJobId);
            if (job == null)
            {
                logger.LogWarning("Cron job {CronJobId} not found", cronJobId);
                return;
            }
            await InternalHandleJobAsync(job, db);
        }

        private async Task ExecuteJobAsync(Crontab job)
        {
            switch (job.Action)
            {
                case "message":
                    await ExecuteMessageAction(job);
                    break;
                case "autoupdate":
                    await ExecuteAutoUpdateAction(job);
                    break;
                case "backup":
                    await ExecuteBackupAction(job);
                    break;
                default:
                    throw new NotImplementedException($"Unknown action: {job.Action}");
            }
        }

        private async Task ExecuteMessageAction(Crontab job)
        {
            logger.LogInformation("Executing message action for cron job {CronJobId}: {Description}", job.Id, job.Description);

            using var scope = scopeFactory.CreateScope();

            // Parse message from Data (JSON)
            string messageText = job.Data ?? string.Empty;
            bool onlyIfNewUserMsg = false;
            if (job.Data?.StartsWith('{') ?? false)
            {
                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Data);
                    if (data?.TryGetValue("message", out var msgValue) == true)
                    {
                        messageText = msgValue?.ToString() ?? job.Data;
                    }
                    if (data?.TryGetValue("onlyHasNewMessages", out var onlyHasNewMessages) == true)
                    {
                        onlyIfNewUserMsg = Convert.ToBoolean(onlyHasNewMessages);
                    }
                    logger.LogDebug("Parsed message action data: message={Message}, onlyIfNewUserMsg={OnlyIfNewUserMsg}", messageText?.Substring(0, Math.Min(50, messageText.Length)), onlyIfNewUserMsg);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse cron job data JSON, using raw data");
                }
            }

            // Add cron tag to message
            string fullMessage = $"[CRON: {job.Description} |  Next-Run: {job.NextExecution:u}]\n{messageText}".Trim();

            // Get target sessions
            List<Guid> sessionIds;
            if (job.SessionId.HasValue && job.SessionId != Guid.Empty)
            {
                sessionIds = [job.SessionId.Value];
                logger.LogDebug("Executing cron job for specific session: {SessionId}", job.SessionId);
            }
            else
            {
                // All sessions
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                sessionIds = await db.ChatSessions.Select(s => s.Id).ToListAsync();
                logger.LogInformation("Executing cron job for all {SessionCount} sessions", sessionIds.Count);
            }

            var sm = scope.ServiceProvider.GetRequiredService<ISessionManager>();
            int processedCount = 0;

            // Send message to each session
            foreach (var sessionId in sessionIds)
            {
                try
                {
                    var sess = await sm.GetSessionAsync(sessionId);
                    if (sess == null)
                    {
                        logger.LogWarning("Session {SessionId} not found", sessionId);
                        continue;
                    }

                    if (onlyIfNewUserMsg)
                    {
                        var last = sess.MessageHistory.LastOrDefault(o => o.IsUser);
                        if (last?.GetTextContent()?.StartsWith("[CRON: ") ?? false)
                        {
                            logger.LogDebug("Skipping session {SessionId}: last message is already from cron", sessionId);
                            continue;
                        }
                    }

                    var cmdContext = sess.Services.GetRequiredService<MessageContextAccessor>().Context;
                    if (cmdContext == null)
                    {
                        logger.LogWarning("No message context available for session {SessionId}", sessionId);
                        continue;
                    }

                    logger.LogDebug("Adding cron message to session {SessionId}", sessionId);
                    sess.MessageHistory.Add(new() { Role = "user", Content = fullMessage });

                    int responseCount = 0;
                    await foreach (var msg in sm.DispatchToLLMAsync(sess, cmdContext))
                    {
                        if (!msg.IsAssistant) continue;
                        var ret = msg.GetTextContent();
                        if (string.IsNullOrWhiteSpace(ret)) continue;
                        if ("NO_REPLY".Equals(ret))
                        {
                            logger.LogDebug("LLM returned NO_REPLY for session {SessionId}", sessionId);
                            continue;
                        }
                        if ("HEARTBEAT_OK".Equals(ret))
                        {
                            logger.LogDebug("LLM returned HEARTBEAT_OK for session {SessionId}", sessionId);
                            continue;
                        }

                        responseCount++;
                        logger.LogInformation("Sending cron response to {ChannelProvider}:{ChannelId} (SessionId: {SessionId})",
                            cmdContext.Channel.ChannelProvider, cmdContext.Channel.ChannelId, sessionId);
                        await cmdContext.Channel.SendChannelAsync(msg);
                    }

                    if (responseCount > 0)
                    {
                        processedCount++;
                        logger.LogDebug("Session {SessionId} processed: {ResponseCount} response(s) sent", sessionId, responseCount);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing cron message for session {SessionId}", sessionId);
                }
            }

            logger.LogInformation("Cron job {CronJobId} completed: {ProcessedCount}/{TotalCount} sessions processed",
                job.Id, processedCount, sessionIds.Count);
        }

        private async Task ExecuteAutoUpdateAction(Crontab job)
        {
            throw new NotImplementedException();
        }

        private async Task ExecuteBackupAction(Crontab job)
        {
            throw new NotImplementedException();
        }
    }
}