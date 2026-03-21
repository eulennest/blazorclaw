using BlazorClaw.Core.Data;
using BlazorClaw.Core.Models;
using BlazorClaw.Core.Services;
using BlazorClaw.Core.Utils;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazorClaw.Server.Services
{
    public class CronJobService(IServiceScopeFactory scopeFactory) : BackgroundService, ICronJobService
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
            throw new NotImplementedException();
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