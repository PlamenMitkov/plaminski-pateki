using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Repositories;

public class AssistantMessageRepository : IAssistantMessageRepository
{
    private readonly AppDbContext _dbContext;

    public AssistantMessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AssistantChatMessage>> GetRecentMessagesAsync(
        int sessionInternalId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 200);

        return await _dbContext.AssistantChatEntries
            .AsNoTracking()
            .Where(item => item.SessionInternalId == sessionInternalId)
            .OrderBy(item => item.CreatedAt)
            .Take(normalizedLimit)
            .Select(item => new AssistantChatMessage
            {
                Role = item.Role,
                Content = item.Content
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SaveConversationTurnAsync(
        AssistantChatSession session,
        string userPrompt,
        string assistantReply,
        string? updatedTitle,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        _dbContext.AssistantChatEntries.Add(new AssistantChatEntry
        {
            SessionInternalId = session.Id,
            Role = "user",
            Content = userPrompt,
            CreatedAt = now
        });

        _dbContext.AssistantChatEntries.Add(new AssistantChatEntry
        {
            SessionInternalId = session.Id,
            Role = "assistant",
            Content = assistantReply,
            CreatedAt = now
        });

        session.LastActivityAt = now;
        if (!string.IsNullOrWhiteSpace(updatedTitle))
        {
            session.Title = updatedTitle;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
