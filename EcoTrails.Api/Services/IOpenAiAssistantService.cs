using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public interface IOpenAiAssistantService
{
    Task<AssistantChatResponse> GenerateReplyAsync(AssistantChatRequest request, string? currentUserId, CancellationToken cancellationToken);
    Task<AssistantSessionResponse> CreateSessionAsync(AssistantSessionCreateRequest request, string? currentUserId, CancellationToken cancellationToken);
    Task<List<AssistantSessionResponse>> GetUserSessionsAsync(string userId, int limit, CancellationToken cancellationToken);
    Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(string sessionId, string? currentUserId, int limit, CancellationToken cancellationToken);
    Task<bool> DeleteSessionAsync(string sessionId, string? currentUserId, CancellationToken cancellationToken);
    Task<AssistantEnrichResponse> EnrichTrailsAsync(AssistantEnrichRequest request, CancellationToken cancellationToken);
    Task<AssistantVectorIndexResponse> IndexTrailsAsync(AssistantVectorIndexRequest request, CancellationToken cancellationToken);
    Task<AssistantVectorSearchResponse> SearchSimilarTrailsAsync(AssistantVectorSearchRequest request, CancellationToken cancellationToken);
}
