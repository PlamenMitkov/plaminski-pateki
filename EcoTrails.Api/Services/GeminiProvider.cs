using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using EcoTrails.Api.Contracts;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public sealed class GeminiProvider(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<GeminiProvider> logger) : IGeminiProvider
{
    private readonly OpenAiOptions _options = options.Value;

    public async Task<string> SendRequestAsync(
        string model,
        string systemInstruction,
        List<AssistantChatMessage> history,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken,
        bool forceJsonResponse = false)
    {
        var apiKey = ResolveApiKey();
        var candidates = BuildGeminiModelCandidates(model);
        AiProviderException? lastException = null;

        foreach (var candidateModel in candidates)
        {
            var endpoint = $"models/{Uri.EscapeDataString(candidateModel)}:generateContent?key={Uri.EscapeDataString(apiKey)}";
            var payload = BuildPayload(systemInstruction, history, userPrompt, temperature, maxTokens, forceJsonResponse);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, AiJsonContext.Default.GeminiPayload), Encoding.UTF8, "application/json")
            };

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(content);
                return ExtractText(document.RootElement);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                lastException = new AiProviderException(404, "Gemini моделът не е наличен.", $"Gemini 404 for {candidateModel}");
                continue;
            }

            await HandleErrorAsync(response, candidateModel, cancellationToken);
        }

        if (lastException is not null) throw lastException;
        throw new InvalidOperationException("Gemini request failed.");
    }

    public async IAsyncEnumerable<string> StreamRequestAsync(
        string model,
        string systemInstruction,
        List<AssistantChatMessage> history,
        string userPrompt,
        double temperature,
        int maxTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var apiKey = ResolveApiKey();
        var endpoint = $"models/{Uri.EscapeDataString(model)}:streamGenerateContent?key={Uri.EscapeDataString(apiKey)}";
        var payload = BuildPayload(systemInstruction, history, userPrompt, temperature, maxTokens, false);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, AiJsonContext.Default.GeminiPayload), Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorAsync(response, model, cancellationToken);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var partialJson = "";
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;
            // Gemini uses server-sent events for streaming, but it's often a JSON array or multiple objects
            // Simple logic here - look for "text"
            if (line.Contains("\"text\""))
            {
                var text = line.Split("\"text\":")[1].Trim().Trim('"').Replace("\\n", "\n");
                yield return text;
            }
        }
    }

    private GeminiPayload BuildPayload(string systemInstruction, List<AssistantChatMessage> history, string userPrompt, double temperature, int maxTokens, bool forceJsonResponse)
    {
        var historyBlock = history.Count == 0 ? "(няма предишни съобщения)" : string.Join("\n", history.Select(h => $"{h.Role}: {h.Content}"));
        var combinedPrompt = $"Инструкции:\n{systemInstruction}\n\nИстория:\n{historyBlock}\n\nТекуща заявка:\n{userPrompt}";

        return new GeminiPayload
        {
            contents = [new GeminiContent { role = "user", parts = [new GeminiPart { text = combinedPrompt }] }],
            generationConfig = new { temperature, maxOutputTokens = maxTokens, responseMimeType = forceJsonResponse ? "application/json" : "text/plain" }
        };
    }

    private string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0) throw new InvalidOperationException("Empty Gemini response.");
        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)) throw new InvalidOperationException("Invalid Gemini response structure.");
        return string.Join("\n", parts.EnumerateArray().Select(p => p.GetProperty("text").GetString()).Where(v => !string.IsNullOrWhiteSpace(v))).Trim();
    }

    private string ResolveApiKey()
    {
        return !string.IsNullOrWhiteSpace(_options.GeminiApiKey) ? _options.GeminiApiKey : Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? _options.ApiKey;
    }

    private List<string> BuildGeminiModelCandidates(string preferredModel)
    {
        return [preferredModel, "gemini-2.5-flash", "gemini-2.0-flash", "gemini-2.0-flash-lite", "gemini-flash-latest"];
    }

    private async Task HandleErrorAsync(HttpResponseMessage response, string model, CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        logger.LogError("Gemini API request failed. Status Code: {StatusCode}. Model: {Model}.", response.StatusCode, model);
        if (statusCode == 429) throw new AiProviderException(429, "Gemini quota limitReached.", $"Gemini 429 for {model}.");
        throw new InvalidOperationException("Проблем при комуникацията с Gemini.");
    }
}
