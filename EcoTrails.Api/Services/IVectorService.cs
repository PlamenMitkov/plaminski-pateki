using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public interface IVectorService
{
    Task<VectorEmbeddingResult> CreateEmbeddingAsync(string input, CancellationToken cancellationToken);
    Task<VectorEmbeddingsBatchResult> CreateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}

public sealed class VectorEmbeddingResult
{
    public string Model { get; set; } = string.Empty;
    public List<float> Values { get; set; } = [];
}

public sealed class VectorEmbeddingsBatchResult
{
    public string Model { get; set; } = string.Empty;
    public List<List<float>> Values { get; set; } = [];
}

public class OpenAiVectorService : IVectorService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiVectorService> _logger;

    public OpenAiVectorService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiVectorService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<VectorEmbeddingResult> CreateEmbeddingAsync(string input, CancellationToken cancellationToken)
    {
        var batch = await CreateEmbeddingsAsync([input], cancellationToken);
        return new VectorEmbeddingResult
        {
            Model = batch.Model,
            Values = batch.Values.FirstOrDefault() ?? []
        };
    }

    public async Task<VectorEmbeddingsBatchResult> CreateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        var normalizedInputs = inputs
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        if (normalizedInputs.Count == 0)
        {
            throw new InvalidOperationException("Embedding input list cannot be empty.");
        }

        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is missing. Set OPENAI_API_KEY or OpenAI__ApiKey.");
        }

        var model = string.IsNullOrWhiteSpace(_options.EmbeddingModel)
            ? "text-embedding-3-small"
            : _options.EmbeddingModel.Trim();

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/embeddings";
        var payload = new
        {
            model,
            input = normalizedInputs
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var correlationId = response.Headers.TryGetValues("x-request-id", out var values)
                ? values.FirstOrDefault() ?? "N/A"
                : "N/A";

            _logger.LogError(
                "OpenAI embeddings request failed. Status Code: {StatusCode}. Correlation ID: {CorrelationId}. Response body redacted for security.",
                response.StatusCode,
                correlationId);

            throw new InvalidOperationException("Грешка при генериране на вектори. Моля, опитайте по-късно.");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var responseModel = root.TryGetProperty("model", out var modelProp)
            ? modelProp.GetString() ?? model
            : model;

        if (!root.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("OpenAI embeddings response does not contain vector data.");
        }

        var vectors = new List<List<float>>();
        foreach (var dataItem in dataProp.EnumerateArray().OrderBy(item => item.GetProperty("index").GetInt32()))
        {
            var embeddingArray = dataItem.GetProperty("embedding");
            if (embeddingArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var values = new List<float>(embeddingArray.GetArrayLength());
            foreach (var item in embeddingArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                values.Add(item.GetSingle());
            }

            if (values.Count > 0)
            {
                vectors.Add(values);
            }
        }

        if (vectors.Count == 0)
        {
            throw new InvalidOperationException("OpenAI embeddings response returned an empty vector.");
        }

        return new VectorEmbeddingsBatchResult
        {
            Model = responseModel,
            Values = vectors
        };
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return _options.ApiKey;
        }

        return Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
    }
}
