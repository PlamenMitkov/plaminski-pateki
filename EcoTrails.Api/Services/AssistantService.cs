using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public sealed partial class AssistantService(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    AppDbContext dbContext,
    IAssistantMessageRepository assistantMessageRepository,
    IAssistantSessionOrchestrationService assistantSessionOrchestrationService,
    IVectorService vectorService,
    IAssistantPromptSafetyService promptSafetyService,
    IAssistantPromptAssemblyService assistantPromptAssemblyService,
    IAssistantProvenancePolicyService provenancePolicyService,
    IAssistantRetrievalService retrievalService,
    IAssistantEnrichmentWorkflowService enrichmentWorkflowService,
    IAssistantResponseCompositionService assistantResponseCompositionService,
    IOpenAiProvider openAiProvider,
    IGeminiProvider geminiProvider,
    IAiProviderFallbackPolicy aiProviderFallbackPolicy,
    IAssistantWeatherContextService weatherContextService,
    ILogger<AssistantService> logger) : IOpenAiAssistantService
{
    private readonly OpenAiOptions _options = options.Value;

    [GeneratedRegex("(ignore\\s+(all|any|previous|above)\\s+instructions|system\\s*prompt|developer\\s*message|role\\s*:\\s*system|jailbreak|reveal\\s+.*(secret|key|token)|<\\s*/?\\s*system\\s*>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromptInjectionRegex();

    public async Task<AssistantVectorIndexResponse> IndexTrailsAsync(
        AssistantVectorIndexRequest request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Trails.AsQueryable();
        if (request.TrailIds is { Count: > 0 })
        {
            query = query.Where(item => request.TrailIds.Contains(item.Id));
        }

        var limit = Math.Clamp(request.Limit ?? 100, 1, 500);
        var trails = await query
            .OrderBy(item => item.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var response = new AssistantVectorIndexResponse();
        var batchSize = Math.Clamp(_options.EmbeddingBatchSize, 1, 50);
        for (var offset = 0; offset < trails.Count; offset += batchSize)
        {
            var batch = trails.Skip(offset).Take(batchSize).ToList();
            var pending = batch
                .Where(item => request.OverwriteExisting || string.IsNullOrWhiteSpace(item.EmbeddingVector))
                .ToList();

            response.Processed += batch.Count;
            if (pending.Count == 0) continue;

            try
            {
                var inputList = pending.Select(BuildTrailEmbeddingInput).ToList();
                var embeddings = await vectorService.CreateEmbeddingsAsync(inputList, cancellationToken);
                var now = DateTime.UtcNow;

                var vectorCount = Math.Min(pending.Count, embeddings.Values.Count);
                for (var index = 0; index < vectorCount; index++)
                {
                    var trail = pending[index];
                    trail.EmbeddingVector = JsonSerializer.Serialize(embeddings.Values[index]);
                    trail.EmbeddingModel = embeddings.Model;
                    trail.EmbeddingUpdatedAt = now;
                    response.Updated++;
                }

                if (embeddings.Values.Count < pending.Count)
                {
                    var missing = pending.Count - embeddings.Values.Count;
                    response.Failed += missing;
                    response.Errors.Add($"Batch starting-offset {offset}: missing {missing} vectors.");
                }
            }
            catch (Exception ex)
            {
                response.Failed += pending.Count;
                response.Errors.Add($"Batch starting-offset {offset}: {ex.Message}");
                logger.LogWarning(ex, "Failed to create embedding batch at offset {Offset}", offset);
            }

            if (_options.EnrichDelayMs > 0) await Task.Delay(_options.EnrichDelayMs, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<AssistantVectorSearchResponse> SearchSimilarTrailsAsync(
        AssistantVectorSearchRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = request.Prompt.Trim();
        var configuredTopK = Math.Clamp(_options.TopK, 1, 10);
        var requestedTopK = request.TopK <= 0 ? configuredTopK : request.TopK;
        var topK = Math.Clamp(requestedTopK, 1, 10);

        var queryEmbedding = await vectorService.CreateEmbeddingAsync(prompt, cancellationToken);

        var query = dbContext.Trails.AsNoTracking()
            .Where(item => !string.IsNullOrWhiteSpace(item.EmbeddingVector));

        if (request.OnlyWithCoordinates)
        {
            query = query.Where(item => item.Latitude.HasValue && item.Longitude.HasValue);
        }

        var candidates = await query
            .Select(item => new TrailSearchCandidate
            {
                Id = item.Id,
                Name = item.Name,
                Location = item.Location,
                Region = item.Region,
                Description = item.Description,
                Difficulty = item.Difficulty,
                DifficultyLevel = item.DifficultyLevel,
                DurationInHours = item.DurationInHours,
                ElevationGain = item.ElevationGain,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                WaterSources = item.WaterSources,
                MaxAltitude = item.MaxAltitude,
                SuitableForKids = item.SuitableForKids,
                RequiredGear = item.RequiredGear,
                EmbeddingVector = item.EmbeddingVector
            })
            .ToListAsync(cancellationToken);

        var matches = candidates
            .Select(item => new
            {
                Trail = item,
                Score = ComputeCosineSimilarity(queryEmbedding.Values, ParseEmbedding(item.EmbeddingVector))
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .Take(topK)
            .Select(item => new AssistantVectorMatch
            {
                Trail = MapToContext(item.Trail),
                Score = Math.Round(item.Score, 6)
            })
            .ToList();

        return new AssistantVectorSearchResponse
        {
            Prompt = prompt,
            Model = queryEmbedding.Model,
            Matches = matches
        };
    }

    public async Task<AssistantChatResponse> GenerateReplyAsync(
        AssistantChatRequest request,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var provider = ResolveProvider();
        var apiKey = ResolveApiKey(provider);
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("API key is missing.");

        var session = await assistantSessionOrchestrationService
            .GetOrCreateSessionAsync(request.SessionId, request.Prompt, currentUserId, cancellationToken);
        var persistedHistory = await assistantMessageRepository
            .GetRecentMessagesAsync(session.Id, 20, cancellationToken);

        var model = ResolveChatModel(provider);
        var prompt = request.Prompt.Trim();
        var isPotentialInjection = _options.PromptInjectionGuardEnabled && promptSafetyService.IsPotentialPromptInjection(prompt);
        var safePrompt = isPotentialInjection ? promptSafetyService.SanitizePrompt(prompt) : prompt;

        if (isPotentialInjection && _options.PromptInjectionBlockOnDetect)
        {
            var blockedReply = "Заявката съдържа потенциално опасни инструкции.";
            await assistantMessageRepository.SaveConversationTurnAsync(session, prompt, blockedReply, null, cancellationToken);
            return new AssistantChatResponse { SessionId = session.SessionId, Reply = blockedReply, Model = model, Provider = provider };
        }

        var isWeatherRequest = weatherContextService.IsWeatherPrompt(prompt);
        var contextTrails = await retrievalService.FindRelevantTrailsAsync(safePrompt, request, cancellationToken);
        var alternatives = await GetAlternativeTrailsAsync(safePrompt, contextTrails, request, cancellationToken);
        if (isWeatherRequest)
        {
            contextTrails = contextTrails.Take(5).ToList();
            alternatives = alternatives.Take(2).ToList();
        }

        var provenanceContext = await BuildProvenanceContextAsync(contextTrails, alternatives, cancellationToken);
        contextTrails = provenanceContext.Trails;
        alternatives = provenanceContext.Alternatives;

        var weatherContext = await weatherContextService.BuildWeatherContextAsync(safePrompt, contextTrails, cancellationToken);
        var history = persistedHistory.Count > 0 ? persistedHistory.TakeLast(14).ToList() : request.History.TakeLast(14).ToList();

        var systemInstruction = assistantPromptAssemblyService.BuildSystemInstruction(_options.AssistantMode, provenanceContext.HasReliabilityWarning, isPotentialInjection);
        var userPrompt = assistantPromptAssemblyService.BuildUserPromptByMode(_options.AssistantMode, request, safePrompt, contextTrails, alternatives, weatherContext, provenanceContext.ReliabilityNote, isPotentialInjection);

        var reply = await SendChatRequestAsync(provider, model, systemInstruction, history, userPrompt, _options.Temperature, _options.MaxTokens, cancellationToken);

        var assistantText = reply?.Trim() ?? string.Empty;
        var updatedTitle = session.Title == "Нова сесия" ? assistantSessionOrchestrationService.BuildSessionTitle(prompt) : null;
        await assistantMessageRepository.SaveConversationTurnAsync(session, prompt, assistantText, updatedTitle, cancellationToken);

        return new AssistantChatResponse
        {
            SessionId = session.SessionId,
            Reply = assistantText,
            Model = model,
            Provider = provider,
            UsedTrails = contextTrails,
            SuggestedAlternativeIds = alternatives.Select(it => it.Id).Distinct().ToList(),
            KnowledgeChips = assistantResponseCompositionService.BuildKnowledgeChips(contextTrails, alternatives, provenanceContext.HasReliabilityWarning, isPotentialInjection),
            QuickActions = assistantResponseCompositionService.BuildQuickActions(contextTrails, alternatives, request, prompt)
        };
    }

    private async Task<string> SendChatRequestAsync(
        string provider,
        string model,
        string systemInstruction,
        List<AssistantChatMessage> history,
        string userPrompt,
        double temperature,
        int maxTokens,
        CancellationToken cancellationToken,
        bool forceJsonResponse = false)
    {
        try
        {
            if (string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase))
            {
                return await openAiProvider.SendRequestAsync(
                    model, systemInstruction, history, userPrompt, temperature, maxTokens, cancellationToken, forceJsonResponse);
            }

            return await geminiProvider.SendRequestAsync(
                model, systemInstruction, history, userPrompt, temperature, maxTokens, cancellationToken, forceJsonResponse);
        }
        catch (AiProviderException ex) when (aiProviderFallbackPolicy.ShouldFallbackToOpenAiFromGemini(ex))
        {
            logger.LogWarning(ex, "Gemini failed. Falling back to OpenAI.");
            return await openAiProvider.SendRequestAsync(
                _options.OpenAiModel ?? "gpt-4o-mini", systemInstruction, history, userPrompt, temperature, maxTokens, cancellationToken, forceJsonResponse);
        }
    }

    public async Task<AssistantSessionResponse> CreateSessionAsync(AssistantSessionCreateRequest request, string? currentUserId, CancellationToken ct) =>
        await assistantSessionOrchestrationService.CreateSessionAsync(request, currentUserId, ct);

    public async Task<List<AssistantSessionResponse>> GetUserSessionsAsync(string userId, int limit, CancellationToken ct) =>
        await assistantSessionOrchestrationService.GetUserSessionsAsync(userId, limit, ct);

    public async Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(string sid, string? uid, int lim, CancellationToken ct) =>
        await assistantSessionOrchestrationService.GetSessionMessagesAsync(sid, uid, lim, ct);

    public async Task<bool> DeleteSessionAsync(string sid, string? uid, CancellationToken ct) =>
        await assistantSessionOrchestrationService.DeleteSessionAsync(sid, uid, ct);

    public async Task<AssistantEnrichResponse> EnrichTrailsAsync(AssistantEnrichRequest req, CancellationToken ct)
    {
        return await enrichmentWorkflowService.ExecuteAsync(req, async (trail, tok) =>
        {
            var system = "Extract structured data from trail text. JSON: { difficulty_level, water_sources, max_altitude, suitable_for_kids, required_gear }";
            var user = $"Name: {trail.Name}\nDescription: {trail.Description}";
            var raw = await SendChatRequestAsync(ResolveProvider(), ResolveChatModel(ResolveProvider()), system, [], user, 0, 256, tok, true);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            return new AssistantTrailSemanticData
            {
                DifficultyLevel = root.TryGetProperty("difficulty_level", out var d) ? d.GetString() : "unknown",
                WaterSources = root.TryGetProperty("water_sources", out var w) && w.GetBoolean(),
                MaxAltitude = root.TryGetProperty("max_altitude", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : null,
                SuitableForKids = root.TryGetProperty("suitable_for_kids", out var k) && k.GetBoolean(),
                RequiredGear = root.TryGetProperty("required_gear", out var g) ? g.EnumerateArray().Select(i => i.GetString() ?? "").ToList() : []
            };
        }, ct);
    }

    private string ResolveApiKey(string provider) =>
        string.Equals(provider, "gemini", StringComparison.OrdinalIgnoreCase)
            ? (!string.IsNullOrWhiteSpace(_options.GeminiApiKey) ? _options.GeminiApiKey : Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? _options.ApiKey)
            : (!string.IsNullOrWhiteSpace(_options.ApiKey) ? _options.ApiKey : Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty);

    private string ResolveProvider() => string.Equals(_options.Provider, "openai", StringComparison.OrdinalIgnoreCase) ? "openai" : "gemini";

    private string ResolveChatModel(string provider) =>
        string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase)
            ? (!string.IsNullOrWhiteSpace(_options.OpenAiModel) ? _options.OpenAiModel : "gpt-4o-mini")
            : (!string.IsNullOrWhiteSpace(_options.GeminiModel) ? _options.GeminiModel : "gemini-2.5-flash");

    private static string BuildTrailEmbeddingInput(Trail t) =>
        $"Име: {t.Name}\nОписание: {t.Description}\nЛокация: {t.Location}\nРегион: {t.Region}\nТрудност: {t.Difficulty}/5\nДенивелация: {t.ElevationGain} м";

    private static IReadOnlyList<float>? ParseEmbedding(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { var p = JsonSerializer.Deserialize<List<float>>(raw); return p is { Count: > 0 } ? p : null; } catch { return null; }
    }

    private static double ComputeCosineSimilarity(IReadOnlyList<float> f, IReadOnlyList<float>? s)
    {
        if (s is null || f.Count == 0 || s.Count == 0) return 0;
        double dot = 0, fNorm = 0, sNorm = 0;
        int dim = Math.Min(f.Count, s.Count);
        for (int i = 0; i < dim; i++) { dot += f[i] * s[i]; fNorm += f[i] * f[i]; sNorm += s[i] * s[i]; }
        return fNorm <= 0 || sNorm <= 0 ? 0 : dot / (Math.Sqrt(fNorm) * Math.Sqrt(sNorm));
    }

    private static AssistantTrailContext MapToContext(TrailSearchCandidate t) => new()
    {
        Id = t.Id, Name = t.Name, Location = t.Location, Region = t.Region, Difficulty = t.Difficulty,
        DurationInHours = t.DurationInHours, ElevationGain = t.ElevationGain,
        HasCoordinates = t.Latitude.HasValue && t.Longitude.HasValue, Latitude = t.Latitude, Longitude = t.Longitude,
        DifficultyLevel = t.DifficultyLevel.ToString().ToLowerInvariant(), WaterSources = t.WaterSources,
        MaxAltitude = t.MaxAltitude, SuitableForKids = t.SuitableForKids,
        RequiredGear = t.RequiredGear?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList() ?? []
    };

    private async Task<ProvenanceContextResult> BuildProvenanceContextAsync(List<AssistantTrailContext> t, List<AssistantTrailContext> a, CancellationToken ct)
    {
        var r = await provenancePolicyService.BuildContextAsync(t, a, ct);
        return new ProvenanceContextResult(r.Trails, r.Alternatives, r.HasReliabilityWarning, r.ReliabilityNote);
    }

    private async Task<List<AssistantTrailContext>> GetAlternativeTrailsAsync(string prompt, List<AssistantTrailContext> primary, AssistantChatRequest req, CancellationToken ct)
    {
        // Placeholder for alternative logic as per existing implementation
        return [];
    }
}