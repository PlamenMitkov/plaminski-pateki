using System.Text.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public sealed class AssistantRetrievalService : IAssistantRetrievalService
{
    private readonly AppDbContext _dbContext;
    private readonly OpenAiOptions _options;
    private readonly IVectorService _vectorService;
    private readonly ILogger<AssistantRetrievalService> _logger;

    public AssistantRetrievalService(
        AppDbContext dbContext,
        IOptions<OpenAiOptions> options,
        IVectorService vectorService,
        ILogger<AssistantRetrievalService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _vectorService = vectorService;
        _logger = logger;
    }

    public async Task<List<AssistantTrailContext>> FindRelevantTrailsAsync(
        string prompt,
        AssistantChatRequest request,
        CancellationToken cancellationToken)
    {
        var maxResults = Math.Clamp(request.MaxContextTrails, 5, 25);
        var hybridTrails = await HybridSearchTrailsAsync(prompt, maxResults, request.OnlyWithCoordinates, cancellationToken);
        var combinedPrompt = BuildCombinedPrompt(prompt, request.FilterSummary);
        var combinedTokens = TrailSearchTextMatcher.ExtractPromptTokens(combinedPrompt);
        var geoPreferenceFromHybrid = BuildGeographicPreference(combinedPrompt, hybridTrails);

        if (hybridTrails.Count > 0)
        {
            var rankedHybrid = hybridTrails
                .Select((item, index) => new
                {
                    Trail = item,
                    Score = ((hybridTrails.Count - index) * 10) + CalculateGeoScore(item, geoPreferenceFromHybrid)
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Trail.Difficulty)
                .ThenBy(item => item.Trail.DurationInHours)
                .Select(item => item.Trail)
                .ToList();

            var localizedHybrid = ApplyLocalityOrdering(rankedHybrid, geoPreferenceFromHybrid)
                .Take(maxResults)
                .Select(MapToContext)
                .ToList();

            return localizedHybrid;
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

        var promptLower = combinedPrompt.ToLowerInvariant();
        var geoPreference = BuildGeographicPreference(combinedPrompt, candidates);

        var ranked = candidates
            .Select(item => new
            {
                Trail = item,
                Score = CalculateScore(item, combinedTokens, promptLower) + CalculateGeoScore(item, geoPreference)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Trail.Difficulty)
            .ThenBy(item => item.Trail.DurationInHours)
            .Select(item => item.Trail)
            .ToList();

        var localizedRanked = ApplyLocalityOrdering(ranked, geoPreference)
            .Take(maxResults)
            .Select(MapToContext)
            .ToList();

        return localizedRanked;
    }

    public async Task<List<AssistantTrailContext>> GetAlternativeTrailsAsync(
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

        foreach (var token in tokens)
        {
            if (TrailSearchTextMatcher.ContainsExactToken(trail.Name, token)) score += 8;
            if (TrailSearchTextMatcher.ContainsExactToken(trail.Location, token)) score += 6;
            if (TrailSearchTextMatcher.ContainsExactToken(trail.Region, token)) score += 6;
            if (TrailSearchTextMatcher.ContainsExactToken(trail.Description, token)) score += 2;
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

    private static string BuildCombinedPrompt(string prompt, string? filterSummary)
    {
        if (string.IsNullOrWhiteSpace(filterSummary))
        {
            return prompt?.Trim() ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(prompt)
            ? filterSummary.Trim()
            : $"{prompt.Trim()} {filterSummary.Trim()}";
    }

    private static GeographicPreference BuildGeographicPreference(
        string combinedPrompt,
        IReadOnlyCollection<TrailSearchCandidate> candidates)
    {
        var preference = new GeographicPreference();
        if (string.IsNullOrWhiteSpace(combinedPrompt) || candidates.Count == 0)
        {
            return preference;
        }

        var tokens = TrailSearchTextMatcher.ExtractPromptTokens(combinedPrompt);
        if (tokens.Count == 0)
        {
            return preference;
        }

        foreach (var token in tokens)
        {
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate.Region) &&
                    TrailSearchTextMatcher.ContainsExactToken(candidate.Region, token))
                {
                    preference.Regions.Add(candidate.Region.Trim());
                }

                if (!string.IsNullOrWhiteSpace(candidate.Location) &&
                    TrailSearchTextMatcher.ContainsExactToken(candidate.Location, token))
                {
                    preference.Locations.Add(candidate.Location.Trim());
                }
            }
        }

        if (!preference.HasExplicitArea)
        {
            return preference;
        }

        var anchor = candidates
            .Where(HasCoordinates)
            .Where(item => MatchesExplicitArea(item, preference))
            .OrderByDescending(item => preference.Locations.Contains(item.Location))
            .ThenByDescending(item => preference.Regions.Contains(item.Region))
            .FirstOrDefault();

        if (anchor is not null)
        {
            preference.AnchorLatitude = anchor.Latitude;
            preference.AnchorLongitude = anchor.Longitude;
        }

        return preference;
    }

    private static IEnumerable<TrailSearchCandidate> ApplyLocalityOrdering(
        IReadOnlyList<TrailSearchCandidate> ranked,
        GeographicPreference preference)
    {
        if (!preference.HasExplicitArea || ranked.Count == 0)
        {
            return ranked;
        }

        var local = ranked.Where(item => IsLocalToPreference(item, preference)).ToList();
        if (local.Count == 0)
        {
            return ranked;
        }

        var localIds = local.Select(item => item.Id).ToHashSet();
        var distant = ranked.Where(item => !localIds.Contains(item.Id));
        return local.Concat(distant);
    }

    private static bool IsLocalToPreference(TrailSearchCandidate trail, GeographicPreference preference)
    {
        if (MatchesExplicitArea(trail, preference))
        {
            return true;
        }

        if (!preference.AnchorLatitude.HasValue || !preference.AnchorLongitude.HasValue)
        {
            return false;
        }

        var distanceKm = ComputeDistanceInKm(
            trail.Latitude,
            trail.Longitude,
            preference.AnchorLatitude,
            preference.AnchorLongitude);

        return distanceKm.HasValue && distanceKm.Value <= 120;
    }

    private static int CalculateGeoScore(TrailSearchCandidate trail, GeographicPreference preference)
    {
        if (!preference.HasExplicitArea)
        {
            return 0;
        }

        var score = 0;
        var isExplicitMatch = MatchesExplicitArea(trail, preference);

        if (isExplicitMatch)
        {
            score += 28;
        }
        else
        {
            score -= 14;
        }

        if (preference.AnchorLatitude.HasValue && preference.AnchorLongitude.HasValue)
        {
            var distanceKm = ComputeDistanceInKm(
                trail.Latitude,
                trail.Longitude,
                preference.AnchorLatitude,
                preference.AnchorLongitude);

            if (distanceKm.HasValue)
            {
                if (distanceKm.Value <= 35)
                {
                    score += 14;
                }
                else if (distanceKm.Value <= 80)
                {
                    score += 8;
                }
                else if (distanceKm.Value <= 140)
                {
                    score += 2;
                }
                else if (distanceKm.Value <= 220)
                {
                    score -= 8;
                }
                else
                {
                    score -= 22;
                }
            }
            else if (!isExplicitMatch)
            {
                score -= 8;
            }
        }

        return score;
    }

    private static bool MatchesExplicitArea(TrailSearchCandidate trail, GeographicPreference preference)
    {
        if (preference.Locations.Count > 0 && preference.Locations.Contains(trail.Location))
        {
            return true;
        }

        return preference.Regions.Count > 0 && preference.Regions.Contains(trail.Region);
    }

    private static bool HasCoordinates(TrailSearchCandidate trail)
    {
        return trail.Latitude.HasValue && trail.Longitude.HasValue;
    }

    private static double? ComputeDistanceInKm(
        double? latitude,
        double? longitude,
        double? referenceLatitude,
        double? referenceLongitude)
    {
        if (!latitude.HasValue || !longitude.HasValue || !referenceLatitude.HasValue || !referenceLongitude.HasValue)
        {
            return null;
        }

        const double earthRadiusKm = 6371;
        var lat1 = ToRadians(latitude.Value);
        var lat2 = ToRadians(referenceLatitude.Value);
        var latDiff = lat2 - lat1;
        var lonDiff = ToRadians(referenceLongitude.Value - longitude.Value);

        var a = Math.Sin(latDiff / 2) * Math.Sin(latDiff / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(lonDiff / 2) * Math.Sin(lonDiff / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * (Math.PI / 180);
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
            Latitude = trail.Latitude,
            Longitude = trail.Longitude,
            DifficultyLevel = trail.DifficultyLevel.ToString().ToLowerInvariant(),
            WaterSources = trail.WaterSources,
            MaxAltitude = trail.MaxAltitude,
            SuitableForKids = trail.SuitableForKids,
            RequiredGear = ParseRequiredGear(trail.RequiredGear)
        };
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
            return parsed?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch
        {
            return raw.Split(',', ';', '|')
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
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

    private sealed class GeographicPreference
    {
        public HashSet<string> Regions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Locations { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool HasExplicitArea => Regions.Count > 0 || Locations.Count > 0;
        public double? AnchorLatitude { get; set; }
        public double? AnchorLongitude { get; set; }
    }
}
