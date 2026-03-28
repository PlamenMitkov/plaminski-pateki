using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public interface IAssistantRetrievalService
{
    Task<List<AssistantTrailContext>> FindRelevantTrailsAsync(
        string prompt,
        AssistantChatRequest request,
        CancellationToken cancellationToken);

    Task<List<AssistantTrailContext>> GetAlternativeTrailsAsync(
        string prompt,
        List<AssistantTrailContext> contextTrails,
        AssistantChatRequest request,
        CancellationToken cancellationToken);
}
