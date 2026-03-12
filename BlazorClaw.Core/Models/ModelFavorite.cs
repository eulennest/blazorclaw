using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Core.Models;

public class ModelFavorite
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public required string InternalName { get; set; }

    public List<string> Aliases { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}