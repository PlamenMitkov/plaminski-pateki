using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Repositories;

public class AssistantSessionReadRepository : IAssistantSessionReadRepository
{
    private readonly AppDbContext _dbContext;

    public AssistantSessionReadRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AssistantSessionResponse>> GetUserSessionsAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 30);

        return await _dbContext.AssistantChatSessions
            .AsNoTracking()
            .Where(item => item.AppUserId == userId)
            .OrderByDescending(item => item.LastActivityAt)
            .Take(normalizedLimit)
            .Select(item => new AssistantSessionResponse
            {
                SessionId = item.SessionId,
                Title = item.Title,
                CreatedAt = item.CreatedAt,
                LastActivityAt = item.LastActivityAt,
                MessageCount = item.Messages.Count,
                IsOwnedByUser = true
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(
        string sessionId,
        string? currentUserId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedSessionId = sessionId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return [];
        }

        var normalizedLimit = Math.Clamp(limit, 1, 200);

        var session = await _dbContext.AssistantChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.SessionId == normalizedSessionId, cancellationToken);

        if (session is null || !CanAccessSession(session, currentUserId))
        {
            return [];
        }

        return await _dbContext.AssistantChatEntries
            .AsNoTracking()
            .Where(item => item.SessionInternalId == session.Id)
            .OrderByDescending(item => item.CreatedAt)
            .Take(normalizedLimit)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new AssistantSessionMessageResponse
            {
                Id = item.Id,
                Role = item.Role,
                Content = item.Content,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private static bool CanAccessSession(AssistantChatSession session, string? currentUserId)
    {
        return !string.IsNullOrWhiteSpace(currentUserId)
            && !string.IsNullOrWhiteSpace(session.AppUserId)
            && string.Equals(session.AppUserId, currentUserId, StringComparison.Ordinal);
    }
}
