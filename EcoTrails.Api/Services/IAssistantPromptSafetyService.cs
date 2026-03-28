namespace EcoTrails.Api.Services;

public interface IAssistantPromptSafetyService
{
    bool IsPotentialPromptInjection(string prompt);
    string SanitizePrompt(string prompt);
}
