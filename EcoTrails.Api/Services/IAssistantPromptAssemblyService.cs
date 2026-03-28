using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public interface IAssistantPromptAssemblyService
{
    string ResolveAssistantMode();

    string BuildSystemInstruction(string mode, bool hasReliabilityWarning, bool isPotentialInjection);

    string BuildUserPromptByMode(
        string mode,
        AssistantChatRequest request,
        string safePrompt,
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives,
        string? weatherContext,
        string? reliabilityNote,
        bool isPotentialInjection);
}
