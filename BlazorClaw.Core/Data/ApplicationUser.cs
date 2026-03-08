using Microsoft.AspNetCore.Identity;

namespace BlazorClaw.Core.Data;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ChannelRegisterToken { get; set; }
    public DateTime? ChannelRegisterTokenExpiredAt { get; set; }
}