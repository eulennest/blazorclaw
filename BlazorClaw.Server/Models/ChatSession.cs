using System.ComponentModel.DataAnnotations;
using BlazorClaw.Server.Data;

namespace BlazorClaw.Server.Models;

public class ChatSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "Neuer Chat";

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
    public virtual ChatSession? Session { get; set; }

    public required string UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }
}
