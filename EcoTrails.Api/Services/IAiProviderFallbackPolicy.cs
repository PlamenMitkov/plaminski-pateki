namespace EcoTrails.Api.Services;

public interface IAiProviderFallbackPolicy
{
    string? ResolveOpenAiFallbackModel(string primaryModel);
    bool ShouldFallbackToSecondaryOpenAiModel(string primaryModel, AiProviderException exception);
    bool ShouldFallbackToOpenAiFromGemini(AiProviderException exception);
}
