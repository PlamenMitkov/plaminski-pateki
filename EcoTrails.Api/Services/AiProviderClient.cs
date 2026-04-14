using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public interface IAiProviderClient
{
    Task<string> SendOpenAiRequestAsync(
        string model,
        string systemInstruction,
        List<AssistantChatMessage> history,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken,
        bool forceJsonResponse = false);

    Task<string> SendGeminiRequestAsync(
        string model,
        string systemInstruction,
        List<AssistantChatMessage> history,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken,
        bool forceJsonResponse = false);
}

public sealed class AiProviderClient(IOpenAiProvider openAiProvider, IGeminiProvider geminiProvider) : IAiProviderClient
{
    public Task<string> SendOpenAiRequestAsync(
        string model,
        string systemInstruction,
        List<AssistantChatMessage> history,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken,
        bool forceJsonResponse = false)
    {
        return openAiProvider.SendRequestAsync(
            model,
            systemInstruction,
            history,
            userPrompt,
            temperature,
            maxTokens,
            cancellationToken,
            forceJsonResponse);
    }

    public Task<string> SendGeminiRequestAsync(
        string model,
        string systemInstruction,
        List<AssistantChatMessage> history,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken,
        bool forceJsonResponse = false)
    {
        return geminiProvider.SendRequestAsync(
            model,
            systemInstruction,
            history,
            userPrompt,
            temperature,
            maxTokens,
            cancellationToken,
            forceJsonResponse);
    }
}
