using BlazorClaw.Core.Data;
using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Core.Models;

public class ChatSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = $"Neuer Chat {DateTime.Now:g}";

    public required string CurrentModel { get; set; }

    public string? ChannelId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }

    // Navigation Property für Teilnehmer (Gruppen-Sitzungen)
    public ICollection<ChatSessionParticipant> Participants { get; set; } = [];
}

public class ChatSessionParticipant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }
    public required string UserId { get; set; }
}


// Database Models

public class RateLimitTracking
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public string? LimitKey { get; set; }
    public int TokenCount { get; set; }
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? Result { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid SessionId { get; set; }
}