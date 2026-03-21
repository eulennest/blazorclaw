using BlazorClaw.Core.Data;
using BlazorClaw.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazorClaw.Server.Services
{
    public class CronJobService(IServiceScopeFactory scopeFactory) : BackgroundService
    {
        TimeSpan MaxDelay = TimeSpan.FromHours(1);
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            var aktDelay = TimeSpan.FromMinutes(1);
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(aktDelay, ct);

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var start = DateTime.UtcNow;
                var list = await db.Crontabs.AsNoTracking().Where(o => o.NextExecution == null || o.NextExecution <= DateTime.UtcNow).ToListAsync(cancellationToken: ct);
                foreach (var job in list)
                {
                    db.ChangeTracker.Clear();
                    try
                    {
                        var cron = Cronos.CronExpression.Parse(job.Cron);
                        job.NextExecution = cron.GetNextOccurrence(DateTime.UtcNow);
                        job.LastExecution = DateTime.UtcNow;
                        db.Crontabs.Update(job);
                        await ExecuteJobAsync(job);
                    }
                    catch (Exception ex)
                    {
                        var al = new AuditLog()
                        {
                            Action = "cron",
                            Details = JsonSerializer.Serialize(job),
                            Result = ex.ToString(),
                            Timestamp = DateTime.UtcNow,
                            SessionId = job.SessionId
                        };
                        db.AuditLogs.Add(al);
                    }
                    await db.SaveChangesAsync(ct);
                }

                var nexteexc = await db.Crontabs.AnyAsync(cancellationToken: ct)
                    ? (await db.Crontabs.MinAsync(o => o.NextExecution, cancellationToken: ct)) ?? DateTime.MaxValue
                    : DateTime.MaxValue;

                aktDelay = TimeSpan.FromSeconds(Math.Min(MaxDelay.TotalSeconds, ((nexteexc - start) + TimeSpan.FromSeconds(10)).TotalSeconds));
            }
        }

        private async Task ExecuteJobAsync(Crontab job)
        {
            throw new NotImplementedException();
        }
    }
}