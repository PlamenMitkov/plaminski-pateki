using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Repositories;

public class AssistantSessionWriteRepository : IAssistantSessionWriteRepository
{
    private readonly AppDbContext _dbContext;

    public AssistantSessionWriteRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AssistantChatSession> CreateSessionAsync(
        string title,
        string? currentUserId,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var session = new AssistantChatSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            AppUserId = string.IsNullOrWhiteSpace(currentUserId) ? null : currentUserId,
            Title = title,
            CreatedAt = createdAtUtc,
            LastActivityAt = createdAtUtc
        };

        _dbContext.AssistantChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return session;
    }

    public async Task<AssistantChatSession?> GetSessionByPublicIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AssistantChatSessions
            .FirstOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);
    }

    public async Task AttachSessionToUserAsync(
        AssistantChatSession session,
        string userId,
        CancellationToken cancellationToken = default)
    {
        session.AppUserId = userId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteSessionIfOwnedByUserAsync(
        string sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.AssistantChatSessions
            .FirstOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);

        if (session is null ||
            string.IsNullOrWhiteSpace(session.AppUserId) ||
            !string.Equals(session.AppUserId, userId, StringComparison.Ordinal))
        {
            return false;
        }

        _dbContext.AssistantChatSessions.Remove(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
