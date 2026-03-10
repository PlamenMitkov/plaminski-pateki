using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Repositories;

public interface IAssistantSessionReadRepository
{
    Task<List<AssistantSessionResponse>> GetUserSessionsAsync(string userId, int limit, CancellationToken cancellationToken = default);
    Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(
        string sessionId,
        string? currentUserId,
        int limit,
        CancellationToken cancellationToken = default);
}
