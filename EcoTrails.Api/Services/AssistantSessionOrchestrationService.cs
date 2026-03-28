using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;

namespace EcoTrails.Api.Services;

public sealed class AssistantSessionOrchestrationService : IAssistantSessionOrchestrationService
{
    private readonly IAssistantSessionReadRepository _assistantSessionReadRepository;
    private readonly IAssistantSessionWriteRepository _assistantSessionWriteRepository;

    public AssistantSessionOrchestrationService(
        IAssistantSessionReadRepository assistantSessionReadRepository,
        IAssistantSessionWriteRepository assistantSessionWriteRepository)
    {
        _assistantSessionReadRepository = assistantSessionReadRepository;
        _assistantSessionWriteRepository = assistantSessionWriteRepository;
    }

    public async Task<AssistantChatSession> GetOrCreateSessionAsync(
        string? sessionId,
        string prompt,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedSessionId = sessionId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            var existingSession = await _assistantSessionWriteRepository
                .GetSessionByPublicIdAsync(normalizedSessionId, cancellationToken);

            if (existingSession is not null)
            {
                if (!CanAccessSession(existingSession, currentUserId))
                {
                    throw new InvalidOperationException("Access denied for this chat session.");
                }

                if (string.IsNullOrWhiteSpace(existingSession.AppUserId) && !string.IsNullOrWhiteSpace(currentUserId))
                {
                    await _assistantSessionWriteRepository
                        .AttachSessionToUserAsync(existingSession, currentUserId, cancellationToken);
                }

                return existingSession;
            }
        }

        return await _assistantSessionWriteRepository.CreateSessionAsync(
            BuildSessionTitle(prompt),
            currentUserId,
            DateTime.UtcNow,
            cancellationToken);
    }

    public string BuildSessionTitle(string prompt)
    {
        var cleaned = prompt?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Нова сесия";
        }

        cleaned = cleaned.Replace('\n', ' ').Replace('\r', ' ');
        if (cleaned.Length <= 64)
        {
            return cleaned;
        }

        return string.Concat(cleaned.AsSpan(0, 61), "...");
    }

    public async Task<AssistantSessionResponse> CreateSessionAsync(
        AssistantSessionCreateRequest request,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Нова сесия" : request.Title.Trim();
        var now = DateTime.UtcNow;
        var session = await _assistantSessionWriteRepository.CreateSessionAsync(title, currentUserId, now, cancellationToken);

        return new AssistantSessionResponse
        {
            SessionId = session.SessionId,
            Title = session.Title,
            CreatedAt = session.CreatedAt,
            LastActivityAt = session.LastActivityAt,
            MessageCount = 0,
            IsOwnedByUser = !string.IsNullOrWhiteSpace(currentUserId)
        };
    }

    public async Task<List<AssistantSessionResponse>> GetUserSessionsAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken)
    {
        return await _assistantSessionReadRepository.GetUserSessionsAsync(userId, limit, cancellationToken);
    }

    public async Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(
        string sessionId,
        string? currentUserId,
        int limit,
        CancellationToken cancellationToken)
    {
        return await _assistantSessionReadRepository.GetSessionMessagesAsync(
            sessionId,
            currentUserId,
            limit,
            cancellationToken);
    }

    public async Task<bool> DeleteSessionAsync(
        string sessionId,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedSessionId = sessionId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSessionId) || string.IsNullOrWhiteSpace(currentUserId))
        {
            return false;
        }

        return await _assistantSessionWriteRepository.DeleteSessionIfOwnedByUserAsync(
            normalizedSessionId,
            currentUserId,
            cancellationToken);
    }

    private static bool CanAccessSession(AssistantChatSession session, string? currentUserId)
    {
        return !string.IsNullOrWhiteSpace(currentUserId)
            && !string.IsNullOrWhiteSpace(session.AppUserId)
            && string.Equals(session.AppUserId, currentUserId, StringComparison.Ordinal);
    }
}
