using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using EcoTrails.Api.Contracts;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public sealed class OpenAiProvider(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiProvider> logger) : IOpenAiProvider
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
        var messages = BuildMessages(systemInstruction, history, userPrompt);
        var payload = new OpenAiPayload
        {
            model = model,
            temperature = temperature,
            max_tokens = maxTokens,
            response_format = forceJsonResponse ? new { type = "json_object" } : null,
            messages = messages
        };

        var apiKey = ResolveApiKey();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, AiJsonContext.Default.OpenAiPayload), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorAsync(response, model, cancellationToken);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);
        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()?.Trim() ?? string.Empty;
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
        var messages = BuildMessages(systemInstruction, history, userPrompt);
        var payload = new
        {
            model,
            temperature,
            max_tokens = maxTokens,
            stream = true,
            messages
        };

        var apiKey = ResolveApiKey();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorAsync(response, model, cancellationToken);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
            if (line.Contains("[DONE]")) break;

            var json = line["data: ".Length..];
            using var document = JsonDocument.Parse(json);
            var delta = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("delta");

            if (delta.TryGetProperty("content", out var content))
            {
                yield return content.GetString() ?? string.Empty;
            }
        }
    }

    private static List<OpenAiMessage> BuildMessages(string systemInstruction, List<AssistantChatMessage> history, string userPrompt)
    {
        var messages = new List<OpenAiMessage> { new() { role = "system", content = systemInstruction } };
        foreach (var item in history)
        {
            var role = item.Role?.Trim().ToLowerInvariant() is "assistant" or "user" ? item.Role.Trim().ToLowerInvariant() : "user";
            messages.Add(new OpenAiMessage { role = role, content = item.Content.Trim() });
        }
        messages.Add(new OpenAiMessage { role = "user", content = userPrompt });
        return messages;
    }

    private string ResolveApiKey()
    {
        return !string.IsNullOrWhiteSpace(_options.ApiKey)
            ? _options.ApiKey
            : Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
    }

    private async Task HandleErrorAsync(HttpResponseMessage response, string model, CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;
        var correlationId = response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault() ?? "N/A" : "N/A";

        logger.LogError(
            "OpenAI API request failed. Status Code: {StatusCode}. Correlation ID: {CorrelationId}. Model: {Model}.",
            response.StatusCode, correlationId, model);

        if (statusCode == 429) throw new AiProviderException(429, "AI услугата е претоварена.", "OpenAI 429.");
        if (statusCode == 404) throw new AiProviderException(404, "Моделът не е наличен.", $"OpenAI 404 for {model}.");
        throw new InvalidOperationException("Проблем при комуникацията с AI услугата.");
    }
}
