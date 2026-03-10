using EcoTrails.Api.Contracts;
using EcoTrails.Api.Services;

namespace EcoTrails.Api.Tests;

public class FakeOpenAiAssistantService : IOpenAiAssistantService
{
    public Task<AssistantChatResponse> GenerateReplyAsync(AssistantChatRequest request, string? currentUserId, CancellationToken cancellationToken)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId!;

        return Task.FromResult(new AssistantChatResponse
        {
            SessionId = sessionId,
            Reply = "fake-response",
            Model = "fake-model",
            Provider = "fake"
        });
    }

    public Task<AssistantSessionResponse> CreateSessionAsync(AssistantSessionCreateRequest request, string? currentUserId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AssistantSessionResponse
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Нова сесия" : request.Title!,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            MessageCount = 0,
            IsOwnedByUser = !string.IsNullOrWhiteSpace(currentUserId)
        });
    }

    public Task<List<AssistantSessionResponse>> GetUserSessionsAsync(string userId, int limit, CancellationToken cancellationToken)
        => Task.FromResult(new List<AssistantSessionResponse>());

    public Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(string sessionId, string? currentUserId, int limit, CancellationToken cancellationToken)
        => Task.FromResult(new List<AssistantSessionMessageResponse>());

    public Task<bool> DeleteSessionAsync(string sessionId, string? currentUserId, CancellationToken cancellationToken)
        => Task.FromResult(true);

    public Task<AssistantEnrichResponse> EnrichTrailsAsync(AssistantEnrichRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new AssistantEnrichResponse());

    public Task<AssistantVectorIndexResponse> IndexTrailsAsync(AssistantVectorIndexRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new AssistantVectorIndexResponse());

    public Task<AssistantVectorSearchResponse> SearchSimilarTrailsAsync(AssistantVectorSearchRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new AssistantVectorSearchResponse
        {
            Prompt = request.Prompt,
            Model = "fake-model"
        });
}
