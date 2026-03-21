using BlazorClaw.Core.Data;
using BlazorClaw.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazorClaw.Server.Services
{
    public class CronJobService(ApplicationDbContext dbContext) : BackgroundService
    {
        TimeSpan MaxDelay = TimeSpan.FromHours(1);
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            var aktDelay = TimeSpan.FromMinutes(1);
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(aktDelay, ct);
                var start = DateTime.UtcNow;
                var list = await dbContext.Crontabs.Where(o => o.NextExecution == null || o.NextExecution <= DateTime.UtcNow).ToListAsync();
                foreach (var job in list)
                {
                    try
                    {
                        var cron = Cronos.CronExpression.Parse(job.Cron);
                        await ExecuteJobAsync(job);
                        job.NextExecution = cron.GetNextOccurrence(DateTime.UtcNow);
                        job.LastExecution = DateTime.UtcNow;
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
                        dbContext.AuditLogs.Add(al);
                    }
                    await dbContext.SaveChangesAsync(ct);
                }

                var nexteexc = (await dbContext.Crontabs.MinAsync(o => o.NextExecution)) ?? DateTime.MaxValue;

                aktDelay = TimeSpan.FromSeconds(Math.Min(MaxDelay.TotalSeconds, ((nexteexc - start) + TimeSpan.FromSeconds(10)).TotalSeconds));
            }
        }

        private async Task ExecuteJobAsync(Crontab job)
        {
            throw new NotImplementedException();
        }
    }
}