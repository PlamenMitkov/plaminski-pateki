using System.Text.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public class TrailProposalReviewService : ITrailProposalReviewService
{
    private readonly IAiProviderClient _aiProviderClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<TrailProposalReviewService> _logger;

    public TrailProposalReviewService(
        IAiProviderClient aiProviderClient,
        IOptions<OpenAiOptions> options,
        ILogger<TrailProposalReviewService> logger)
    {
        _aiProviderClient = aiProviderClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CommunityPostAiReviewResponse> EvaluateAsync(CommunityTrailPost post, CancellationToken cancellationToken)
    {
        var fallback = BuildFallbackReview(post);
        var provider = string.Equals(_options.Provider, "openai", StringComparison.OrdinalIgnoreCase)
            ? "openai"
            : "gemini";

        var systemInstruction = "Ти си анализатор за верификация на туристически сигнали. " +
                                "Оцени дали това е достоверно предложение за нова екопътека в България. " +
                                "Отговаряй само с JSON.";
        var prompt =
            "Върни JSON с полета: isLikelyTrailProposal (bool), reliabilityScore (0-100 int), summary (string), " +
            "suggestedName (string), suggestedLocation (string), suggestedRegion (string), suggestedDifficultyLevel (Easy|Moderate|Difficult), warnings (string[]).\n" +
            $"Заглавие: {post.Title}\n" +
            $"Съдържание: {post.Content}";

        try
        {
            var model = provider == "openai" ? _options.OpenAiModel : _options.GeminiModel;
            var raw = provider == "openai"
                ? await _aiProviderClient.SendOpenAiRequestAsync(
                    model, systemInstruction, [], prompt, 0.1, 500, cancellationToken, forceJsonResponse: true)
                : await _aiProviderClient.SendGeminiRequestAsync(
                    model, systemInstruction, [], prompt, 0.1, 500, cancellationToken, forceJsonResponse: true);

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            var score = root.TryGetProperty("reliabilityScore", out var scoreProp) && scoreProp.TryGetInt32(out var parsedScore)
                ? Math.Clamp(parsedScore, 0, 100)
                : fallback.ReliabilityScore;

            var warnings = root.TryGetProperty("warnings", out var warningsProp) && warningsProp.ValueKind == JsonValueKind.Array
                ? warningsProp.EnumerateArray()
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList()
                : fallback.Warnings.ToList();

            return new CommunityPostAiReviewResponse
            {
                IsLikelyTrailProposal = root.TryGetProperty("isLikelyTrailProposal", out var proposalProp)
                    ? proposalProp.ValueKind == JsonValueKind.True
                    : fallback.IsLikelyTrailProposal,
                ReliabilityScore = score,
                Summary = root.TryGetProperty("summary", out var summaryProp)
                    ? summaryProp.GetString() ?? fallback.Summary
                    : fallback.Summary,
                SuggestedName = root.TryGetProperty("suggestedName", out var nameProp)
                    ? nameProp.GetString() ?? fallback.SuggestedName
                    : fallback.SuggestedName,
                SuggestedLocation = root.TryGetProperty("suggestedLocation", out var locationProp)
                    ? locationProp.GetString() ?? fallback.SuggestedLocation
                    : fallback.SuggestedLocation,
                SuggestedRegion = root.TryGetProperty("suggestedRegion", out var regionProp)
                    ? regionProp.GetString() ?? fallback.SuggestedRegion
                    : fallback.SuggestedRegion,
                SuggestedDifficultyLevel = root.TryGetProperty("suggestedDifficultyLevel", out var difficultyProp)
                    ? difficultyProp.GetString() ?? fallback.SuggestedDifficultyLevel
                    : fallback.SuggestedDifficultyLevel,
                Warnings = warnings,
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "AI review fallback used for community post {PostId}.", post.Id);
            return fallback;
        }
    }

    public CommunityPostAiReviewResponse BuildFallbackReview(CommunityTrailPost post)
    {
        var content = $"{post.Title} {post.Content}".ToLowerInvariant();
        var hasRouteSignal = content.Contains("екопътека") || content.Contains("маршрут") || content.Contains("пътека");
        var hasSpecifics = content.Contains("км") || content.Contains("час") || content.Contains("кота") || content.Contains("координат");
        var score = 35;
        if (hasRouteSignal) score += 30;
        if (hasSpecifics) score += 20;
        if (post.Content.Length > 180) score += 10;

        return new CommunityPostAiReviewResponse
        {
            IsLikelyTrailProposal = hasRouteSignal,
            ReliabilityScore = Math.Clamp(score, 0, 100),
            Summary = hasRouteSignal
                ? "Постът прилича на предложение за нов маршрут и изисква админ проверка."
                : "Постът няма достатъчно данни, че описва нова пътека.",
            SuggestedName = post.Title,
            SuggestedLocation = string.Empty,
            SuggestedRegion = string.Empty,
            SuggestedDifficultyLevel = "Moderate",
            Warnings = hasSpecifics
                ? []
                : ["Липсват конкретни параметри като дължина, денивелация или координати."],
        };
    }
}
