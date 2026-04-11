using System.ComponentModel.DataAnnotations;

namespace BlazorClaw.Core.Models;

public class OAuthServer
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(1000)]
    public string? WellKnownUrl { get; set; }

    [Required]
    [MaxLength(500)]
    public required string ClientId { get; set; }

    [MaxLength(2000)]
    public string? ClientSecret { get; set; }

    [Required]
    [MaxLength(2000)]
    public required string Scopes { get; set; }

    [Required]
    [MaxLength(1000)]
    public required string RedirectUri { get; set; }

    [Required]
    [MaxLength(1000)]
    public required string TokenEndpoint { get; set; }

    [Required]
    [MaxLength(1000)]
    public required string AuthEndpoint { get; set; }

    public virtual ICollection<OAuthToken> OAuthTokens { get; set; } = [];
}
