using System.Text;
using System.Text.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public class OpenAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-3.5-turbo";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 500;
    public int EnrichDelayMs { get; set; } = 200;
    public int RetryAttempts { get; set; } = 3;
    public int RetryInitialDelayMs { get; set; } = 500;
    public int RetryJitterMs { get; set; } = 200;
    public int EmbeddingBatchSize { get; set; } = 20;
    public int RrfK { get; set; } = 60;
    public int VectorMultiplier { get; set; } = 2;
    public int TopK { get; set; } = 5;
}

public class OpenAiAssistantService : IOpenAiAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<OpenAiAssistantService> _logger;
    private readonly IVectorService _vectorService;
    private readonly IAssistantSessionReadRepository _assistantSessionReadRepository;
    private readonly IAssistantSessionWriteRepository _assistantSessionWriteRepository;
    private readonly IAssistantMessageRepository _assistantMessageRepository;

    public OpenAiAssistantService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        AppDbContext dbContext,
        IAssistantSessionReadRepository assistantSessionReadRepository,
        IAssistantSessionWriteRepository assistantSessionWriteRepository,
        IAssistantMessageRepository assistantMessageRepository,
        IVectorService vectorService,
        ILogger<OpenAiAssistantService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _dbContext = dbContext;
        _assistantSessionReadRepository = assistantSessionReadRepository;
        _assistantSessionWriteRepository = assistantSessionWriteRepository;
        _assistantMessageRepository = assistantMessageRepository;
        _vectorService = vectorService;
        _logger = logger;
    }

    public async Task<AssistantVectorIndexResponse> IndexTrailsAsync(
        AssistantVectorIndexRequest request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Trails.AsQueryable();
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
            if (pending.Count == 0)
            {
                continue;
            }

            try
            {
                var inputList = pending.Select(BuildTrailEmbeddingInput).ToList();
                var embeddings = await _vectorService.CreateEmbeddingsAsync(inputList, cancellationToken);
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
                    response.Errors.Add($"Batch starting at trail offset {offset}: missing {missing} embedding vectors.");
                }
            }
            catch (Exception exception)
            {
                response.Failed += pending.Count;
                response.Errors.Add($"Batch starting at trail offset {offset}: {exception.Message}");
                _logger.LogWarning(exception, "Failed to create embedding batch at offset {Offset}", offset);
            }

            if (_options.EnrichDelayMs > 0)
            {
                await Task.Delay(_options.EnrichDelayMs, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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

        var queryEmbedding = await _vectorService.CreateEmbeddingAsync(prompt, cancellationToken);

        var query = _dbContext.Trails.AsNoTracking()
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
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is missing. Set OPENAI_API_KEY or OpenAI__ApiKey.");
        }

        var session = await GetOrCreateSessionAsync(request.SessionId, request.Prompt, currentUserId, cancellationToken);
        var persistedHistory = await _assistantMessageRepository
            .GetRecentMessagesAsync(session.Id, 20, cancellationToken);

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-3.5-turbo" : _options.Model;
        var prompt = request.Prompt.Trim();
        var contextTrails = await FindRelevantTrailsAsync(prompt, request, cancellationToken);
        var alternatives = await GetAlternativeTrailsAsync(prompt, contextTrails, request, cancellationToken);
        var userPrompt = BuildUserPrompt(request, contextTrails, alternatives);
        var requestHistory = request.History
            .Where(item => !string.IsNullOrWhiteSpace(item.Content))
            .TakeLast(6)
            .ToList();
        var history = persistedHistory.Count > 0 ? persistedHistory.TakeLast(6).ToList() : requestHistory;

        var messages = new List<object>
        {
            new
            {
                role = "system",
                content =
                    "Ти си Еко-Асистент за планински екопътеки в България. Отговаряй на български език. " +
                    "Логика: 1) Ползвай само контекста от подадените пътеки. 2) Ако difficulty_level е difficult, " +
                    "добави предупреждение за физическа подготовка. 3) Ако water_sources е false, задължително " +
                    "препоръчай носене на вода. 4) Завършвай с конкретно действие според required_gear. " +
                    "Бъди практичен и дай 2-3 конкретни маршрута, когато има достатъчно данни."
            }
        };

        foreach (var item in history)
        {
            var role = item.Role?.Trim().ToLowerInvariant();
            if (role is not ("assistant" or "user"))
            {
                role = "user";
            }

            messages.Add(new
            {
                role,
                content = item.Content.Trim()
            });
        }

        messages.Add(new
        {
            role = "user",
            content = userPrompt
        });

        var payload = new
        {
            model,
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            messages
        };

        var content = await SendOpenAiRequestAsync(payload, cancellationToken);

        using var document = JsonDocument.Parse(content);
        var reply = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        var assistantText = reply?.Trim() ?? string.Empty;
        var updatedTitle = session.Title == "Нова сесия" ? BuildSessionTitle(prompt) : null;

        await _assistantMessageRepository.SaveConversationTurnAsync(
            session,
            prompt,
            assistantText,
            updatedTitle,
            cancellationToken);

        return new AssistantChatResponse
        {
            SessionId = session.SessionId,
            Reply = assistantText,
            Model = model,
            Provider = "openai",
            UsedTrails = contextTrails,
            SuggestedAlternatives = alternatives,
            SuggestedAlternativeIds = alternatives.Select(item => item.Id).Distinct().ToList(),
            KnowledgeChips = BuildKnowledgeChips(contextTrails, alternatives),
            QuickActions = BuildQuickActions(contextTrails, alternatives)
        };
    }

    public async Task<AssistantSessionResponse> CreateSessionAsync(
        AssistantSessionCreateRequest request,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Нова сесия" : request.Title.Trim();
        var now = DateTime.UtcNow;
        var session = await _assistantSessionWriteRepository.CreateSessionAsync(title, currentUserId, now, cancellationToken);

        return new AssistantSessionResponse
        {
            SessionId = session.SessionId,
            Title = session.Title,
            CreatedAt = session.CreatedAt,
            LastActivityAt = session.LastActivityAt,
            MessageCount = 0,
            IsOwnedByUser = !string.IsNullOrWhiteSpace(currentUserId)
        };
    }

    public async Task<List<AssistantSessionResponse>> GetUserSessionsAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken)
    {
        return await _assistantSessionReadRepository.GetUserSessionsAsync(userId, limit, cancellationToken);
    }

    public async Task<List<AssistantSessionMessageResponse>> GetSessionMessagesAsync(
        string sessionId,
        string? currentUserId,
        int limit,
        CancellationToken cancellationToken)
    {
        return await _assistantSessionReadRepository.GetSessionMessagesAsync(
            sessionId,
            currentUserId,
            limit,
            cancellationToken);
    }

    public async Task<bool> DeleteSessionAsync(
        string sessionId,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedSessionId = sessionId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSessionId) || string.IsNullOrWhiteSpace(currentUserId))
        {
            return false;
        }

        return await _assistantSessionWriteRepository.DeleteSessionIfOwnedByUserAsync(
            normalizedSessionId,
            currentUserId,
            cancellationToken);
    }

    public async Task<AssistantEnrichResponse> EnrichTrailsAsync(AssistantEnrichRequest request, CancellationToken cancellationToken)
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is missing. Set OPENAI_API_KEY or OpenAI__ApiKey.");
        }

        var query = _dbContext.Trails.AsQueryable();
        if (request.TrailIds is { Count: > 0 })
        {
            query = query.Where(item => request.TrailIds.Contains(item.Id));
        }

        var limit = Math.Clamp(request.Limit ?? 50, 1, 300);
        var trails = await query
            .OrderBy(item => item.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var response = new AssistantEnrichResponse();

        foreach (var trail in trails)
        {
            response.Processed++;

            if (!request.OverwriteExisting && HasSemanticData(trail))
            {
                continue;
            }

            try
            {
                var extracted = await ExtractSemanticDataAsync(trail, cancellationToken);
                trail.DifficultyLevel = extracted.DifficultyLevel;
                trail.WaterSources = extracted.WaterSources;
                trail.MaxAltitude = extracted.MaxAltitude;
                trail.SuitableForKids = extracted.SuitableForKids;
                trail.RequiredGear = JsonSerializer.Serialize(extracted.RequiredGear);
                response.Updated++;
            }
            catch (Exception exception)
            {
                response.Failed++;
                response.Errors.Add($"Trail {trail.Id}: {exception.Message}");
                _logger.LogWarning(exception, "Failed to enrich trail {TrailId}", trail.Id);
            }

            if (_options.EnrichDelayMs > 0)
            {
                await Task.Delay(_options.EnrichDelayMs, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return _options.ApiKey;
        }

        return Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
    }

    private async Task<List<AssistantTrailContext>> FindRelevantTrailsAsync(
        string prompt,
        AssistantChatRequest request,
        CancellationToken cancellationToken)
    {
        var maxResults = Math.Clamp(request.MaxContextTrails, 5, 25);
        var hybridTrails = await HybridSearchTrailsAsync(prompt, maxResults, request.OnlyWithCoordinates, cancellationToken);
        if (hybridTrails.Count > 0)
        {
            return hybridTrails.Select(MapToContext).ToList();
        }

        var query = _dbContext.Trails.AsNoTracking().AsQueryable();

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
                RequiredGear = item.RequiredGear
            })
            .ToListAsync(cancellationToken);

        var tokens = prompt
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToLowerInvariant())
            .Where(item => item.Length >= 3)
            .Distinct()
            .ToList();

        var promptLower = prompt.ToLowerInvariant();

        var ranked = candidates
            .Select(item => new
            {
                Trail = item,
                Score = CalculateScore(item, tokens, promptLower)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Trail.Difficulty)
            .ThenBy(item => item.Trail.DurationInHours)
            .Take(maxResults)
            .Select(item => MapToContext(item.Trail))
            .ToList();

        return ranked;
    }

    private async Task<List<TrailSearchCandidate>> HybridSearchTrailsAsync(
        string query,
        int topK = 5,
        bool onlyWithCoordinates = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var configuredTopK = Math.Clamp(_options.TopK, 1, 25);
        var effectiveTopK = topK <= 0 ? configuredTopK : topK;
        var normalizedTopK = Math.Clamp(effectiveTopK, 1, 25);
        var vectorMultiplier = Math.Clamp(_options.VectorMultiplier, 1, 10);
        var fetchLimit = Math.Clamp(normalizedTopK * vectorMultiplier, normalizedTopK, 100);

        var vectorTask = GetVectorSearchResultsAsync(normalizedQuery, fetchLimit, onlyWithCoordinates, cancellationToken);
        var textTask = GetFullTextSearchResultsAsync(normalizedQuery, fetchLimit, onlyWithCoordinates, cancellationToken);

        await Task.WhenAll(vectorTask, textTask);

        var vectorResults = await vectorTask;
        var textResults = await textTask;

        if (vectorResults.Count == 0 && textResults.Count == 0)
        {
            return [];
        }

        var rrfK = Math.Clamp(_options.RrfK, 1, 500);
        var scores = new Dictionary<int, ScoredTrail>();

        for (var index = 0; index < vectorResults.Count; index++)
        {
            var trail = vectorResults[index];
            var score = 1.0 / (rrfK + index + 1);
            scores[trail.Id] = new ScoredTrail
            {
                Trail = trail,
                CombinedScore = score
            };
        }

        for (var index = 0; index < textResults.Count; index++)
        {
            var trail = textResults[index];
            var score = 1.0 / (rrfK + index + 1);
            if (scores.TryGetValue(trail.Id, out var existing))
            {
                existing.CombinedScore += score;
            }
            else
            {
                scores[trail.Id] = new ScoredTrail
                {
                    Trail = trail,
                    CombinedScore = score
                };
            }
        }

        return scores.Values
            .OrderByDescending(item => item.CombinedScore)
            .ThenBy(item => item.Trail.Difficulty)
            .ThenBy(item => item.Trail.DurationInHours)
            .Take(normalizedTopK)
            .Select(item => item.Trail)
            .ToList();
    }

    private async Task<List<TrailSearchCandidate>> GetVectorSearchResultsAsync(
        string query,
        int limit,
        bool onlyWithCoordinates,
        CancellationToken cancellationToken)
    {
        VectorEmbeddingResult promptEmbedding;
        try
        {
            promptEmbedding = await _vectorService.CreateEmbeddingAsync(query, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to create prompt embedding. Vector part of hybrid search will be skipped.");
            return [];
        }

        var trailsQuery = _dbContext.Trails.AsNoTracking()
            .Where(item => !string.IsNullOrWhiteSpace(item.EmbeddingVector));

        if (onlyWithCoordinates)
        {
            trailsQuery = trailsQuery.Where(item => item.Latitude.HasValue && item.Longitude.HasValue);
        }

        var candidates = await trailsQuery
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

        if (candidates.Count == 0)
        {
            return [];
        }

        return candidates
            .Select(item => new
            {
                Trail = item,
                Score = ComputeCosineSimilarity(promptEmbedding.Values, ParseEmbedding(item.EmbeddingVector))
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Trail.Difficulty)
            .ThenBy(item => item.Trail.DurationInHours)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(item => item.Trail)
            .ToList();
    }

    private async Task<List<TrailSearchCandidate>> GetFullTextSearchResultsAsync(
        string query,
        int limit,
        bool onlyWithCoordinates,
        CancellationToken cancellationToken)
    {
        try
        {
            var searchQuery = _dbContext.Trails
                .FromSqlInterpolated($"SELECT * FROM [Trails] WHERE FREETEXT((Name, Description, Location), {query})")
                .AsNoTracking()
                .AsQueryable();

            if (onlyWithCoordinates)
            {
                searchQuery = searchQuery.Where(item => item.Latitude.HasValue && item.Longitude.HasValue);
            }

            var items = await searchQuery
                .Take(Math.Clamp(limit, 1, 100))
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

            return items;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Full-text search unavailable. Text part of hybrid search will be skipped.");
            return [];
        }
    }

    private static string BuildTrailEmbeddingInput(Trail trail)
    {
        return $"Име: {trail.Name}\n" +
               $"Описание: {trail.Description}\n" +
               $"Локация: {trail.Location}\n" +
               $"Регион: {trail.Region}\n" +
               $"Трудност: {trail.Difficulty}/5 ({trail.DifficultyLevel})\n" +
               $"Продължителност: {trail.DurationInHours:F1} часа\n" +
               $"Денивелация: {trail.ElevationGain} м\n" +
               $"Водоизточници: {(trail.WaterSources ? "да" : "не")}\n" +
               $"Подходяща за деца: {(trail.SuitableForKids ? "да" : "не")}";
    }

    private static IReadOnlyList<float>? ParseEmbedding(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<float>>(raw);
            return parsed is { Count: > 0 } ? parsed : null;
        }
        catch
        {
            return null;
        }
    }

    private static double ComputeCosineSimilarity(IReadOnlyList<float> first, IReadOnlyList<float>? second)
    {
        if (second is null || first.Count == 0 || second.Count == 0)
        {
            return 0;
        }

        var dimensions = Math.Min(first.Count, second.Count);
        if (dimensions == 0)
        {
            return 0;
        }

        double dot = 0;
        double firstNorm = 0;
        double secondNorm = 0;

        for (var index = 0; index < dimensions; index++)
        {
            var firstValue = first[index];
            var secondValue = second[index];
            dot += firstValue * secondValue;
            firstNorm += firstValue * firstValue;
            secondNorm += secondValue * secondValue;
        }

        if (firstNorm <= 0 || secondNorm <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(firstNorm) * Math.Sqrt(secondNorm));
    }

    private static int CalculateScore(TrailSearchCandidate trail, List<string> tokens, string prompt)
    {
        var score = 0;
        var name = trail.Name.ToLowerInvariant();
        var location = trail.Location.ToLowerInvariant();
        var region = trail.Region.ToLowerInvariant();
        var description = trail.Description.ToLowerInvariant();

        foreach (var token in tokens)
        {
            if (name.Contains(token)) score += 8;
            if (location.Contains(token)) score += 6;
            if (region.Contains(token)) score += 6;
            if (description.Contains(token)) score += 2;
        }

        if (prompt.Contains("деца") || prompt.Contains("семейств"))
        {
            score += trail.SuitableForKids ? 10 : -2;
        }

        if (prompt.Contains("вода") || prompt.Contains("чешм"))
        {
            score += trail.WaterSources ? 6 : -2;
        }

        if (prompt.Contains("труд") || prompt.Contains("предизвик"))
        {
            score += trail.DifficultyLevel == TrailDifficultyLevel.Difficult ? 7 : 1;
        }

        if (prompt.Contains("лека") || prompt.Contains("начинаещ"))
        {
            score += trail.DifficultyLevel == TrailDifficultyLevel.Easy ? 7 : -1;
        }

        if (prompt.Contains("карта") || prompt.Contains("координ"))
        {
            score += trail.Latitude.HasValue && trail.Longitude.HasValue ? 5 : 0;
        }

        return score;
    }

    private static AssistantTrailContext MapToContext(TrailSearchCandidate trail)
    {
        return new AssistantTrailContext
        {
            Id = trail.Id,
            Name = trail.Name,
            Location = trail.Location,
            Region = trail.Region,
            Difficulty = trail.Difficulty,
            DurationInHours = trail.DurationInHours,
            ElevationGain = trail.ElevationGain,
            HasCoordinates = trail.Latitude.HasValue && trail.Longitude.HasValue,
            DifficultyLevel = trail.DifficultyLevel.ToString().ToLowerInvariant(),
            WaterSources = trail.WaterSources,
            MaxAltitude = trail.MaxAltitude,
            SuitableForKids = trail.SuitableForKids,
            RequiredGear = ParseRequiredGear(trail.RequiredGear)
        };
    }

    private static string BuildUserPrompt(
        AssistantChatRequest request,
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives)
    {
        var prompt = string.IsNullOrWhiteSpace(request.Prompt)
            ? "Дай ми препоръка за маршрут."
            : request.Prompt.Trim();

        var sb = new StringBuilder();
        sb.AppendLine($"Въпрос: {prompt}");

        if (!string.IsNullOrWhiteSpace(request.FilterSummary))
        {
            sb.AppendLine($"Активни филтри: {request.FilterSummary}");
        }

        if (alternatives.Count > 0)
        {
            sb.AppendLine("Алтернативи (по-леки/по-подходящи):");
            foreach (var alternative in alternatives)
            {
                sb.AppendLine(
                    $"- {alternative.Name} | {alternative.Location} | регион: {(string.IsNullOrWhiteSpace(alternative.Region) ? "няма данни" : alternative.Region)} | трудност {alternative.Difficulty}/5 ({alternative.DifficultyLevel}) | " +
                    $"подходяща за деца: {(alternative.SuitableForKids ? "да" : "не")} | " +
                    $"вода: {(alternative.WaterSources ? "да" : "не")}");
            }
        }

        sb.AppendLine($"Брой любими: {request.FavoriteCount}");

        if (trails.Count == 0)
        {
            sb.AppendLine("Няма налични маршрути в текущия контекст.");
        }
        else
        {
            sb.AppendLine("Маршрути в контекста:");
            foreach (var trail in trails)
            {
                sb.AppendLine(
                    $"- {trail.Name} | {trail.Location} | регион: {(string.IsNullOrWhiteSpace(trail.Region) ? "няма данни" : trail.Region)} | трудност {trail.Difficulty}/5 ({trail.DifficultyLevel}) | " +
                    $"{trail.DurationInHours:F1} ч | {trail.ElevationGain} м | " +
                    $"вода: {(trail.WaterSources ? "да" : "не")} | " +
                    $"подходяща за деца: {(trail.SuitableForKids ? "да" : "не")} | " +
                    $"макс. височина: {(trail.MaxAltitude.HasValue ? trail.MaxAltitude.Value.ToString() : "няма данни")} м | " +
                    $"координати: {(trail.HasCoordinates ? "да" : "не")} | " +
                    $"екипировка: {(trail.RequiredGear.Count > 0 ? string.Join(", ", trail.RequiredGear) : "няма данни")}");
            }
        }

        sb.AppendLine("Отговори с кратък анализ и 2-3 конкретни предложения.");
        return sb.ToString();
    }

    private async Task<ExtractedSemanticData> ExtractSemanticDataAsync(Trail trail, CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-3.5-turbo" : _options.Model;
        var payload = new
        {
            model,
            temperature = 0,
            max_tokens = 260,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new
                {
                    role = "system",
                    content =
                        "Извличаш структурирани данни за планинска пътека. Връщай САМО валиден JSON с ключове: " +
                        "difficulty_level (easy|moderate|difficult), water_sources (boolean), max_altitude (integer|null), " +
                        "suitable_for_kids (boolean), required_gear (array of strings)."
                },
                new
                {
                    role = "user",
                    content =
                        $"Име: {trail.Name}\n" +
                        $"Локация: {trail.Location}\n" +
                        $"Описание: {trail.Description}\n" +
                        $"Трудност(1-5): {trail.Difficulty}\n" +
                        $"Денивелация: {trail.ElevationGain} м\n" +
                        $"Продължителност: {trail.DurationInHours} часа\n"
                }
            }
        };

        var content = await SendOpenAiRequestAsync(payload, cancellationToken);
        using var document = JsonDocument.Parse(content);
        var raw = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Empty extraction content.");
        }

        using var extractedDoc = JsonDocument.Parse(raw);
        var root = extractedDoc.RootElement;

        var difficultyRaw = root.TryGetProperty("difficulty_level", out var difficultyProp)
            ? difficultyProp.GetString() ?? string.Empty
            : string.Empty;

        var difficulty = NormalizeDifficultyLevel(difficultyRaw);

        var waterSources = root.TryGetProperty("water_sources", out var waterProp) && waterProp.ValueKind == JsonValueKind.True
            ? true
            : root.TryGetProperty("water_sources", out waterProp) && waterProp.ValueKind == JsonValueKind.False
                ? false
                : false;

        int? maxAltitude = null;
        if (root.TryGetProperty("max_altitude", out var altitudeProp) && altitudeProp.ValueKind == JsonValueKind.Number)
        {
            maxAltitude = altitudeProp.GetInt32();
        }

        var suitableForKids = root.TryGetProperty("suitable_for_kids", out var kidsProp) && kidsProp.ValueKind == JsonValueKind.True;

        var gear = new List<string>();
        if (root.TryGetProperty("required_gear", out var gearProp) && gearProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in gearProp.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        gear.Add(value.Trim());
                    }
                }
            }
        }

        if (gear.Count == 0)
        {
            gear.Add("туристически обувки");
            gear.Add("вода");
        }

        return new ExtractedSemanticData
        {
            DifficultyLevel = difficulty,
            WaterSources = waterSources,
            MaxAltitude = maxAltitude,
            SuitableForKids = suitableForKids,
            RequiredGear = gear.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private async Task<string> SendOpenAiRequestAsync(object payload, CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";
        var attempts = Math.Clamp(_options.RetryAttempts, 1, 6);
        var initialDelayMs = Math.Clamp(_options.RetryInitialDelayMs, 100, 5000);
        var jitterMs = Math.Clamp(_options.RetryJitterMs, 0, 2000);
        var apiKey = ResolveApiKey();

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    return content;
                }

                var statusCode = (int)response.StatusCode;
                var correlationId = response.Headers.TryGetValues("x-request-id", out var values)
                    ? values.FirstOrDefault() ?? "N/A"
                    : "N/A";
                var isTransient = statusCode == 429 || statusCode == 503 || statusCode >= 500;
                if (!isTransient || attempt == attempts)
                {
                    _logger.LogError(
                        "OpenAI API request failed. Status Code: {StatusCode}. Correlation ID: {CorrelationId}. Attempt: {Attempt}/{Attempts}. Response body redacted for security.",
                        response.StatusCode,
                        correlationId,
                        attempt,
                        attempts);
                    throw new InvalidOperationException("Възникна проблем при комуникацията с AI услугата. Моля, опитайте отново по-късно.");
                }

                var retryAfter = response.Headers.RetryAfter?.Delta;
                var computedDelayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                var baseDelayMs = Math.Min(computedDelayMs, 8000);
                var jitterDelayMs = jitterMs > 0 ? Random.Shared.Next(0, jitterMs + 1) : 0;
                var delay = retryAfter ?? TimeSpan.FromMilliseconds(baseDelayMs + jitterDelayMs);

                _logger.LogWarning(
                    "Transient OpenAI API error. Status Code: {StatusCode}. Correlation ID: {CorrelationId}. Attempt: {Attempt}/{Attempts}. Retrying in {DelayMs} ms. Response body redacted for security.",
                    response.StatusCode,
                    correlationId,
                    attempt,
                    attempts,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
                continue;
            }
            catch (HttpRequestException exception) when (attempt < attempts)
            {
                var computedDelayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                var baseDelayMs = Math.Min(computedDelayMs, 8000);
                var jitterDelayMs = jitterMs > 0 ? Random.Shared.Next(0, jitterMs + 1) : 0;
                var delay = TimeSpan.FromMilliseconds(baseDelayMs + jitterDelayMs);
                _logger.LogWarning(
                    exception,
                    "Network error during OpenAI request on attempt {Attempt}/{Attempts}. Retrying in {DelayMs} ms.",
                    attempt,
                    attempts,
                    (int)delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
                continue;
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested && attempt < attempts)
            {
                var computedDelayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                var baseDelayMs = Math.Min(computedDelayMs, 8000);
                var jitterDelayMs = jitterMs > 0 ? Random.Shared.Next(0, jitterMs + 1) : 0;
                var delay = TimeSpan.FromMilliseconds(baseDelayMs + jitterDelayMs);
                _logger.LogWarning(
                    exception,
                    "Timeout during OpenAI request on attempt {Attempt}/{Attempts}. Retrying in {DelayMs} ms.",
                    attempt,
                    attempts,
                    (int)delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
                continue;
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Network error during OpenAI request on final attempt {Attempt}/{Attempts}.",
                    attempt,
                    attempts);
                throw new InvalidOperationException("Възникна проблем при комуникацията с AI услугата. Моля, опитайте отново по-късно.");
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    exception,
                    "Timeout during OpenAI request on final attempt {Attempt}/{Attempts}.",
                    attempt,
                    attempts);
                throw new InvalidOperationException("Възникна проблем при комуникацията с AI услугата. Моля, опитайте отново по-късно.");
            }
        }

        throw new InvalidOperationException("Възникна проблем при комуникацията с AI услугата. Моля, опитайте отново по-късно.");
    }

    private static bool HasSemanticData(Trail trail)
    {
        return trail.MaxAltitude.HasValue ||
               trail.WaterSources ||
               trail.SuitableForKids ||
               !string.IsNullOrWhiteSpace(trail.RequiredGear) && trail.RequiredGear != "[]";
    }

    private async Task<AssistantChatSession> GetOrCreateSessionAsync(
        string? sessionId,
        string prompt,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var normalizedSessionId = sessionId?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            var existingSession = await _assistantSessionWriteRepository
                .GetSessionByPublicIdAsync(normalizedSessionId, cancellationToken);

            if (existingSession is not null)
            {
                if (!CanAccessSession(existingSession, currentUserId))
                {
                    throw new InvalidOperationException("Access denied for this chat session.");
                }

                if (string.IsNullOrWhiteSpace(existingSession.AppUserId) && !string.IsNullOrWhiteSpace(currentUserId))
                {
                    await _assistantSessionWriteRepository
                        .AttachSessionToUserAsync(existingSession, currentUserId, cancellationToken);
                }

                return existingSession;
            }
        }

        return await _assistantSessionWriteRepository.CreateSessionAsync(
            BuildSessionTitle(prompt),
            currentUserId,
            DateTime.UtcNow,
            cancellationToken);
    }

    private static bool CanAccessSession(AssistantChatSession session, string? currentUserId)
    {
        return !string.IsNullOrWhiteSpace(currentUserId)
            && !string.IsNullOrWhiteSpace(session.AppUserId)
            && string.Equals(session.AppUserId, currentUserId, StringComparison.Ordinal);
    }

    private static TrailDifficultyLevel NormalizeDifficultyLevel(string? raw)
    {
        var normalized = raw?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized is "easy" or "лесно" or "лека" or "light" or "beginner")
        {
            return TrailDifficultyLevel.Easy;
        }

        if (normalized is "difficult" or "hard" or "трудно" or "трудна" or "тежко" or "тежка")
        {
            return TrailDifficultyLevel.Difficult;
        }

        return TrailDifficultyLevel.Moderate;
    }

    private static string BuildSessionTitle(string prompt)
    {
        var clean = string.IsNullOrWhiteSpace(prompt) ? "Нова сесия" : prompt.Trim();
        if (clean.Length <= 90)
        {
            return clean;
        }

        return clean[..90];
    }

    private static List<string> ParseRequiredGear(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw);
            return parsed?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToList() ?? [];
        }
        catch
        {
            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }
    }

    private static List<AssistantKnowledgeChip> BuildKnowledgeChips(
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives)
    {
        var chips = new List<AssistantKnowledgeChip>();
        if (trails.Count == 0)
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Няма намерени пътеки", Type = "info" });
            return chips;
        }

        if (trails.Any(item => item.DifficultyLevel == "difficult"))
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Има трудни маршрути в контекста", Type = "warning" });
        }

        if (trails.Any(item => !item.WaterSources))
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Има маршрути без водоизточници", Type = "warning" });
        }

        if (trails.Any(item => item.SuitableForKids))
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Налични са маршрути, подходящи за деца", Type = "positive" });
        }

        if (trails.Any(item => item.HasCoordinates))
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Налични са маршрути с координати", Type = "info" });
        }

        var commonGear = trails
            .SelectMany(item => item.RequiredGear)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .Take(2)
            .ToList();

        chips.AddRange(commonGear.Select(item => new AssistantKnowledgeChip
        {
            Label = $"Препоръчителна екипировка: {item}",
            Type = "gear"
        }));

        if (alternatives.Count > 0)
        {
            chips.Add(new AssistantKnowledgeChip
            {
                Label = BuildAddedAlternativesLabel(alternatives.Count),
                Type = "positive"
            });

            var primaryRegion = trails[0].Region?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(primaryRegion))
            {
                var neighboringRegionCount = alternatives
                    .Where(item => !string.IsNullOrWhiteSpace(item.Region))
                    .Count(item => !string.Equals(item.Region.Trim(), primaryRegion, StringComparison.OrdinalIgnoreCase));

                if (neighboringRegionCount > 0)
                {
                    chips.Add(new AssistantKnowledgeChip
                    {
                        Label = BuildNearbyAlternativesLabel(neighboringRegionCount),
                        Type = "info"
                    });
                }
            }

            foreach (var alternative in alternatives.Take(2))
            {
                chips.Add(new AssistantKnowledgeChip
                {
                    Label = $"По-лека алтернатива: {alternative.Name}",
                    Type = "info"
                });
            }
        }

        return chips;
    }

    private static string BuildAddedAlternativesLabel(int count)
    {
        return count == 1
            ? "Добавена 1 алтернатива"
            : $"Добавени {count} алтернативи";
    }

    private static string BuildNearbyAlternativesLabel(int count)
    {
        return count == 1
            ? "Добавена 1 близка алтернатива"
            : $"Добавени {count} близки алтернативи";
    }

    private static List<AssistantQuickAction> BuildQuickActions(
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives)
    {
        var actions = new List<AssistantQuickAction>();
        var trailWithCoords = trails.FirstOrDefault(item => item.HasCoordinates);
        if (trailWithCoords is not null)
        {
            actions.Add(new AssistantQuickAction
            {
                Id = "show-map",
                Label = "Покажи ми маршрута на картата",
                Value = trailWithCoords.Id.ToString()
            });
        }

        var firstTrail = trails.FirstOrDefault();
        if (firstTrail is not null)
        {
            actions.Add(new AssistantQuickAction
            {
                Id = "weather-now",
                Label = "Какво е времето там сега?",
                Value = firstTrail.Location
            });
        }

        foreach (var alternative in alternatives)
        {
            actions.Add(new AssistantQuickAction
            {
                Id = "open-trail-details",
                Label = $"Покажи ми детайли за {alternative.Name}",
                Value = alternative.Id.ToString()
            });
        }

        return actions;
    }

    private async Task<List<AssistantTrailContext>> GetAlternativeTrailsAsync(
        string prompt,
        List<AssistantTrailContext> contextTrails,
        AssistantChatRequest request,
        CancellationToken cancellationToken)
    {
        if (contextTrails.Count == 0)
        {
            return [];
        }

        var primaryTrail = contextTrails[0];
        var favoriteSet = request.FavoriteTrailIds?.ToHashSet() ?? [];
        var isPrimaryNotFavorite = favoriteSet.Count > 0 && !favoriteSet.Contains(primaryTrail.Id);
        var hasConcern = HasDifficultyConcern(prompt);
        var shouldSuggestAlternatives =
            hasConcern ||
            isPrimaryNotFavorite ||
            string.Equals(primaryTrail.DifficultyLevel, "difficult", StringComparison.OrdinalIgnoreCase);

        if (!shouldSuggestAlternatives)
        {
            return [];
        }

        var sameRegionCandidates = await _dbContext.Trails
            .AsNoTracking()
            .Where(item => item.Id != primaryTrail.Id)
            .Where(item => !string.IsNullOrWhiteSpace(primaryTrail.Region)
                ? item.Region == primaryTrail.Region
                : item.Location == primaryTrail.Location)
            .Where(item => item.DifficultyLevel == TrailDifficultyLevel.Easy || item.DifficultyLevel == TrailDifficultyLevel.Moderate)
            .OrderBy(item => item.Difficulty)
            .ThenBy(item => item.DurationInHours)
            .ThenBy(item => item.ElevationGain)
            .Take(6)
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
                RequiredGear = item.RequiredGear
            })
            .ToListAsync(cancellationToken);

        var alternatives = sameRegionCandidates
            .Select(MapToContext)
            .Take(3)
            .ToList();

        if (alternatives.Count >= 3)
        {
            return alternatives;
        }

        var primaryCoordinates = await _dbContext.Trails
            .AsNoTracking()
            .Where(item => item.Id == primaryTrail.Id)
            .Select(item => new { item.Latitude, item.Longitude })
            .FirstOrDefaultAsync(cancellationToken);

        var fallbackCandidates = await _dbContext.Trails
            .AsNoTracking()
            .Where(item => item.Id != primaryTrail.Id)
            .Where(item => item.DifficultyLevel == TrailDifficultyLevel.Easy || item.DifficultyLevel == TrailDifficultyLevel.Moderate)
            .Where(item => string.IsNullOrWhiteSpace(primaryTrail.Region) || item.Region != primaryTrail.Region)
            .OrderBy(item => item.Difficulty)
            .ThenBy(item => item.DurationInHours)
            .ThenBy(item => item.ElevationGain)
            .Take(60)
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
                RequiredGear = item.RequiredGear
            })
            .ToListAsync(cancellationToken);

        var existingIds = alternatives.Select(item => item.Id).ToHashSet();
        var remainingSlots = 3 - alternatives.Count;

        var rankedFallback = fallbackCandidates
            .Where(item => !existingIds.Contains(item.Id))
            .OrderBy(item => ComputeCoordinateDistanceSquared(
                item.Latitude,
                item.Longitude,
                primaryCoordinates?.Latitude,
                primaryCoordinates?.Longitude))
            .ThenBy(item => item.Difficulty)
            .ThenBy(item => item.DurationInHours)
            .ThenBy(item => item.ElevationGain)
            .Take(remainingSlots)
            .Select(MapToContext)
            .ToList();

        alternatives.AddRange(rankedFallback);

        return alternatives;
    }

    private static double ComputeCoordinateDistanceSquared(
        double? latitude,
        double? longitude,
        double? referenceLatitude,
        double? referenceLongitude)
    {
        if (!latitude.HasValue || !longitude.HasValue || !referenceLatitude.HasValue || !referenceLongitude.HasValue)
        {
            return double.MaxValue;
        }

        var latDiff = latitude.Value - referenceLatitude.Value;
        var lonDiff = longitude.Value - referenceLongitude.Value;
        return (latDiff * latDiff) + (lonDiff * lonDiff);
    }

    private static bool HasDifficultyConcern(string prompt)
    {
        var normalized = prompt.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("притесня") ||
               normalized.Contains("трудно") ||
               normalized.Contains("умора") ||
               normalized.Contains("по-лек") ||
               normalized.Contains("по лек") ||
               normalized.Contains("нещо по-лесно") ||
               normalized.Contains("начинаещ");
    }

    private sealed class TrailSearchCandidate
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Difficulty { get; set; }
        public TrailDifficultyLevel DifficultyLevel { get; set; }
        public double DurationInHours { get; set; }
        public int ElevationGain { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool WaterSources { get; set; }
        public int? MaxAltitude { get; set; }
        public bool SuitableForKids { get; set; }
        public string RequiredGear { get; set; } = "[]";
        public string? EmbeddingVector { get; set; }
    }

    private sealed class ScoredTrail
    {
        public TrailSearchCandidate Trail { get; set; } = new();
        public double CombinedScore { get; set; }
    }

    private sealed class ExtractedSemanticData
    {
        public TrailDifficultyLevel DifficultyLevel { get; set; }
        public bool WaterSources { get; set; }
        public int? MaxAltitude { get; set; }
        public bool SuitableForKids { get; set; }
        public List<string> RequiredGear { get; set; } = [];
    }
}