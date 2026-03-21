using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Core.Models;

public class Crontab
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required string Description { get; set; }
    public Guid? SessionId { get; set; }
    public ChatSession? Session { get; set; }

    [Required]
    public required string Cron { get; set; }

    [Required]
    public required string Action { get; set; }
    public string? Data { get; set; }
    public bool System { get; set; }
    public DateTime? NextExecution { get; set; }
    public DateTime? LastExecution { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}