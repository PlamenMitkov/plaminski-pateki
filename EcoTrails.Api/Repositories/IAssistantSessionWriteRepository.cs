using EcoTrails.Api.Models;

namespace EcoTrails.Api.Repositories;

public interface IAssistantSessionWriteRepository
{
    Task<AssistantChatSession> CreateSessionAsync(string title, string? currentUserId, DateTime createdAtUtc, CancellationToken cancellationToken = default);
    Task<AssistantChatSession?> GetSessionByPublicIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task AttachSessionToUserAsync(AssistantChatSession session, string userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSessionIfOwnedByUserAsync(string sessionId, string userId, CancellationToken cancellationToken = default);
}
