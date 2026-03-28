using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public interface IAssistantResponseCompositionService
{
    List<AssistantKnowledgeChip> BuildKnowledgeChips(
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives,
        bool hasReliabilityWarning,
        bool isPotentialInjection);

    List<AssistantQuickAction> BuildQuickActions(
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives,
        AssistantChatRequest request,
        string prompt);
}
