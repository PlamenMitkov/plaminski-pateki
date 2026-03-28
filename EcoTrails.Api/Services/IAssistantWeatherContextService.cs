using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public interface IAssistantWeatherContextService
{
    bool IsWeatherPrompt(string prompt);

    Task<string?> BuildWeatherContextAsync(
        string prompt,
        List<AssistantTrailContext> trails,
        CancellationToken cancellationToken);
}
