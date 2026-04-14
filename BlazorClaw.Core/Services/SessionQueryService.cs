using BlazorClaw.Core.Data;
using BlazorClaw.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorClaw.Core.Services;

public interface ISessionQueryService
{
    Task<List<ChatSession>> GetSessionsForUserAsync(string userId);
    Task<List<ChatSession>> GetAllSessionsAsync();
}

public class SessionQueryService(ApplicationDbContext context) : ISessionQueryService
{
    public async Task<List<ChatSession>> GetSessionsForUserAsync(string userId)
    {
        return await context.ChatSessions
            .Include(s => s.Participants)
            .Where(s => s.UserId == userId || s.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(s => s.LastUsedAt)
            .ToListAsync();
    }

    public async Task<List<ChatSession>> GetAllSessionsAsync()
    {
        return await context.ChatSessions
            .Include(s => s.Participants)
            .OrderByDescending(s => s.LastUsedAt)
            .ToListAsync();
    }
}
