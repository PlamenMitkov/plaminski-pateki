using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;

namespace EcoTrails.Api.Repositories;

public interface IAssistantMessageRepository
{
    Task<List<AssistantChatMessage>> GetRecentMessagesAsync(
        int sessionInternalId,
        int limit,
        CancellationToken cancellationToken = default);

    Task SaveConversationTurnAsync(
        AssistantChatSession session,
        string userPrompt,
        string assistantReply,
        string? updatedTitle,
        CancellationToken cancellationToken = default);
}
