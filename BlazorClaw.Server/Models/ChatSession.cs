using System.ComponentModel.DataAnnotations;
using BlazorClaw.Server.Data;

namespace BlazorClaw.Server.Models;

public class ChatSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "Neuer Chat";
    
    public string CurrentModel { get; set; }
    
    public string? ChannelId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsArchived { get; set; }

    // Navigation Property für Teilnehmer (Gruppen-Sitzungen)
    public ICollection<ChatSessionParticipant> Participants { get; set; } = new List<ChatSessionParticipant>();
}

public class ChatSessionParticipant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid SessionId { get; set; }
    public ChatSession Session { get; set; }
    
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
}
