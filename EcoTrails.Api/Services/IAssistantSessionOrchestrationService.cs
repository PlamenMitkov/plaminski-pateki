using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;

namespace EcoTrails.Api.Services;

public interface IAssistantSessionOrchestrationService
{
    Task<AssistantChatSession> GetOrCreateSessionAsync(
        string? sessionId,
        string prompt,
        string? currentUserId,
        CancellationToken cancellationToken);

    string BuildSessionTitle(string prompt);

    Task<AssistantSessionResponse> CreateSessionAsync(
        AssistantSessionCreateRequest request,
        string? currentUserId,
        CancellationToken cancellationToken);

    Task<List<AssistantSessionResponse>> GetUserSessionsAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken);

    Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(
        string sessionId,
        string? currentUserId,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> DeleteSessionAsync(
        string sessionId,
        string? currentUserId,
        CancellationToken cancellationToken);
}
