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
            var aktDelay = TimeSpan.FromMinutes(1);
            while (!ct.IsCancellationRequested)
            {
                if (cancellationTokenSource.IsCancellationRequested) cancellationTokenSource = new();
                await Task.Delay(aktDelay, cancellationTokenSource.Token).NoThrow();
                if (ct.IsCancellationRequested) break;

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var start = DateTime.UtcNow;
                var list = await db.Crontabs.AsNoTracking().Where(o => o.NextExecution == null || o.NextExecution <= DateTime.UtcNow).ToListAsync(cancellationToken: ct);
                foreach (var job in list)
                {
                    db.ChangeTracker.Clear();
                    db.Crontabs.Update(job);
                    await InternalHandleJobAsync(job, db);
                }

                var nexteexc = await db.Crontabs.AnyAsync(cancellationToken: ct)
                    ? (await db.Crontabs.MinAsync(o => o.NextExecution, cancellationToken: ct)) ?? DateTime.MaxValue
                    : DateTime.MaxValue;

                aktDelay = TimeSpan.FromSeconds(Math.Min(MaxDelay.TotalSeconds, ((nexteexc - start) + TimeSpan.FromSeconds(10)).TotalSeconds));
            }
        }

        private async Task InternalHandleJobAsync(Crontab job, ApplicationDbContext db)
        {
            try
            {
                var cron = Cronos.CronExpression.Parse(job.Cron);
                job.LastExecution = DateTime.UtcNow;
                job.NextExecution = cron.GetNextOccurrence(DateTime.UtcNow);

                await ExecuteJobAsync(job);
            }
            catch (Exception ex)
            {
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
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var job = await db.Crontabs.FindAsync(cronJobId);
            if (job == null) return;
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
            using var scope = scopeFactory.CreateScope();

            // Parse message from Data (JSON)
            string messageText = job.Data ?? string.Empty;
            bool onlyIfNewUserMsg = false;
            if (!string.IsNullOrEmpty(job.Data))
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
                }
                catch { }
            }

            // Add cron tag to message
            string fullMessage = $"[CRON: {job.Description} |  Next-Run: {job.NextExecution:u}]\n{messageText}".Trim();

            // Get target sessions
            List<Guid> sessionIds;
            if (job.SessionId.HasValue && job.SessionId != Guid.Empty)
            {
                sessionIds = [job.SessionId.Value];
            }
            else
            {
                // All sessions
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                sessionIds = await db.ChatSessions.Select(s => s.Id).ToListAsync();
            }

            var sm = scope.ServiceProvider.GetRequiredService<ISessionManager>();
            // Send message to each session
            foreach (var sessionId in sessionIds)
            {
                var sess = await sm.GetSessionAsync(sessionId);
                if (sess != null)
                {
                    if (onlyIfNewUserMsg)
                    {
                        var last = sess.MessageHistory.LastOrDefault(o => o.IsUser);
                        if (last?.GetTextContent()?.StartsWith("[CRON: ") ?? false)
                            continue;
                    }

                    var cmdContext = sess.Services.GetRequiredService<MessageContextAccessor>().Context;
                    if (cmdContext == null) return;
                    sess.MessageHistory.Add(new() { Role = "user", Content = fullMessage });
                    await foreach (var msg in sm.DispatchToLLMAsync(sess, cmdContext))
                    {
                        if (!msg.IsAssistant) continue;
                        var ret = msg.GetTextContent();
                        if (string.IsNullOrWhiteSpace(ret)) continue;
                        if ("NO_REPLY".Equals(ret)) continue;
                        if ("HEARTBEAT_OK".Equals(ret)) continue;


                        logger.LogInformation("Sending reply to {ChannelProvider}:{ChannelId} : {content}", cmdContext.Channel.ChannelProvider, cmdContext.Channel.ChannelId, msg.Content);
                        await cmdContext.Channel.SendChannelAsync(msg);
                    }
                }
            }

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