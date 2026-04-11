using BlazorClaw.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazorClaw.Core.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatSessionParticipant> ChatSessionParticipants { get; set; }
    public DbSet<ModelFavorite> ModelFavorites { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<OAuthToken> OAuthTokens { get; set; }
    public DbSet<OAuthServer> OAuthServers { get; set; }

    public DbSet<RateLimitTracking> RateLimitTrackings { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Crontab> Crontabs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customize ASP.NET Identity tables
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(r => r.Description).HasMaxLength(500);
        });

        // RateLimitTracking
        builder.Entity<RateLimitTracking>(entity =>
        {
            entity.HasIndex(r => new { r.UserId, r.LimitKey, r.Timestamp });
            entity.HasIndex(r => new { r.UserId, r.ToolName, r.Timestamp });
            entity.HasIndex(r => r.Timestamp); // Für Cleanup
        });

        // AuditLog
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => new { a.UserId, a.Timestamp });
            entity.HasIndex(a => new { a.Action, a.Timestamp });
            entity.HasIndex(a => a.SessionId);
            entity.HasIndex(a => a.Timestamp); // Für Cleanup
        });

        builder.Entity<ApiKey>(entity =>
        {
            entity.HasIndex(a => new { a.Identifier, a.UserId }).IsUnique();
            entity.HasOne(a => a.OAuthToken)
                .WithOne()
                .HasForeignKey<ApiKey>(a => a.OAuthTokenId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OAuthToken>(entity =>
        {
            entity.HasIndex(o => o.ServerId);
            entity.HasOne(o => o.Server)
                .WithMany(s => s.OAuthTokens)
                .HasForeignKey(o => o.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}