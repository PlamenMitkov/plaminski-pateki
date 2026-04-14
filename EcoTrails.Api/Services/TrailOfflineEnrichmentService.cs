using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace EcoTrails.Api.Services;

public sealed partial class TrailOfflineEnrichmentService(
    IMemoryCache cache,
    AppDbContext dbContext,
    IWebHostEnvironment environment,
    IHttpClientFactory httpClientFactory,
    ITrailRepository trailRepository,
    IConfiguration configuration,
    ILogger<TrailOfflineEnrichmentService> logger) : ITrailOfflineEnrichmentService
{
    private static readonly TimeSpan TrailCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan SourcePreviewCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan AlertsCacheDuration = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] PreferredDatasetFiles = ["ecoupdated.json", "eco.json"];
    private static readonly HashSet<string> PlaceholderRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Пловдив",
        "София",
        "Смолян",
        "България",
        "Неуточнено"
    };

    [GeneratedRegex("<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("<meta\\s+name=[\"']description[\"']\\s+content=[\"'](.*?)[\"']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DescriptionRegex();

    [GeneratedRegex("(жълт|оранжев|червен)\\s+код[^\\n<]*", RegexOptions.IgnoreCase)]
    private static partial Regex AlertLineRegex();

    public async Task<TrailOfflineEnrichmentResponse> GetOfflineEnrichmentAsync(
        IReadOnlyList<Trail> trails,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.UtcNow;
        var ecoIndex = await GetEcoTrailIndexAsync(cancellationToken);
        var enriched = new List<TrailOfflineEnrichmentItem>(trails.Count);
        var cachedTrailCount = 0;
        var sourcePreviewCount = 0;
        var sourcePreviewFailures = 0;
        var alerts = await GetLiveWeatherAlertsAsync(cancellationToken);

        foreach (var trail in trails)
        {
            var cacheKey = $"trail-offline-enrichment:{trail.Id}";
            if (cache.TryGetValue(cacheKey, out TrailOfflineEnrichmentItem? cached) && cached is not null)
            {
                cachedTrailCount++;
                enriched.Add(cached);
                continue;
            }

            var persistedSnapshot = await TryGetSnapshotFromDatabaseAsync(trail.Id, cancellationToken);
            if (persistedSnapshot is not null)
            {
                cache.Set(cacheKey, persistedSnapshot, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TrailCacheDuration
                });
                cachedTrailCount++;
                if (persistedSnapshot.SourcePreview is not null)
                {
                    sourcePreviewCount++;
                }

                enriched.Add(persistedSnapshot);
                continue;
            }

            var key = BuildKey(trail.Name, trail.Location);
            ecoIndex.TryGetValue(key, out var metadata);

            TrailSourcePreview? sourcePreview = null;
            if (!string.IsNullOrWhiteSpace(metadata?.SourceUrl))
            {
                var previewResult = await TryGetSourcePreviewAsync(metadata.SourceUrl!, cancellationToken);
                sourcePreview = previewResult.Preview;
                if (sourcePreview is not null)
                {
                    sourcePreviewCount++;
                }

                if (previewResult.Failed)
                {
                    sourcePreviewFailures++;
                }
            }

            var item = BuildItem(trail, metadata, sourcePreview, generatedAt);
            cache.Set(cacheKey, item, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TrailCacheDuration
            });

            await SaveSnapshotAsync(item, cancellationToken);
            enriched.Add(item);
        }

        return new TrailOfflineEnrichmentResponse
        {
            GeneratedAt = generatedAt,
            RequestedTrailCount = trails.Count,
            EnrichedTrailCount = enriched.Count,
            CachedTrailCount = cachedTrailCount,
            SourcePreviewCount = sourcePreviewCount,
            SourcePreviewFailures = sourcePreviewFailures,
            WeatherAlerts = alerts,
            Trails = enriched,
        };
    }

    public async Task WarmDailyCacheAsync(CancellationToken cancellationToken = default)
    {
        var trails = await trailRepository.ExportTrailsAsync(
            new TrailQueryParameters
            {
                SortBy = "id",
                SortDirection = "asc"
            },
            ids: null,
            cancellationToken: cancellationToken);

        var toWarm = trails.Take(250).ToList();
        var response = await GetOfflineEnrichmentAsync(toWarm, cancellationToken);

        logger.LogInformation(
            "Offline enrichment cache warmup completed. Requested={Requested}, Enriched={Enriched}, Cached={Cached}, SourcePreviewFailures={SourcePreviewFailures}",
            response.RequestedTrailCount,
            response.EnrichedTrailCount,
            response.CachedTrailCount,
            response.SourcePreviewFailures);
    }

    private async Task<Dictionary<string, EcoTrailMetadata>> GetEcoTrailIndexAsync(CancellationToken cancellationToken)
    {
        const string ecoIndexCacheKey = "trail-offline-enrichment:eco-index";
        if (cache.TryGetValue(ecoIndexCacheKey, out Dictionary<string, EcoTrailMetadata>? cached) && cached is not null)
        {
            return cached;
        }

        var workspaceRoot = Directory.GetParent(environment.ContentRootPath)?.FullName;
        if (workspaceRoot is null)
        {
            return [];
        }

        var jsonPath = ResolveDatasetPath(workspaceRoot);
        if (jsonPath is null)
        {
            logger.LogWarning("No eco dataset file was found. Checked: {Files}", string.Join(", ", PreferredDatasetFiles));
            return [];
        }

        await using var stream = File.OpenRead(jsonPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("eco_trails", out var trailsElement) || trailsElement.ValueKind != JsonValueKind.Array)
        {
            logger.LogWarning("{JsonPath} does not contain a valid eco_trails array", jsonPath);
            return [];
        }

        var index = new Dictionary<string, EcoTrailMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var trailElement in trailsElement.EnumerateArray())
        {
            var name = GetString(trailElement, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var nearestTown = GetNestedString(trailElement, "location", "nearest_town");
            var region = GetNestedString(trailElement, "location", "region");
            var location = ResolveLocation(name, nearestTown, region);

            var metadata = new EcoTrailMetadata
            {
                SourceUrl = GetString(trailElement, "source"),
                EquipmentNeeded = GetStringArray(trailElement, "equipment_needed"),
                SafetyWarnings = GetStringArray(trailElement, "safety_warnings"),
                NearbyAmenities = GetStringArray(trailElement, "nearby_amenities"),
                SuitabilityTags = GetStringArray(trailElement, "suitability"),
                BestMonths = GetNestedStringArray(trailElement, "seasonal_info", "best_months"),
                PublicTransport = GetNestedString(trailElement, "transportation", "public_transport"),
                ParkingAvailable = GetNestedBool(trailElement, "transportation", "parking_available"),
                WheelchairAccessible = GetNestedBool(trailElement, "accessibility", "wheelchair_accessible"),
                StrollerFriendly = GetNestedBool(trailElement, "accessibility", "stroller_friendly"),
                BicycleAllowed = GetNestedBool(trailElement, "accessibility", "bicycle_allowed"),
                WinterAccessible = GetNestedBool(trailElement, "seasonal_info", "winter_accessible"),
                WeatherDependent = GetNestedBool(trailElement, "seasonal_info", "weather_dependent"),
            };

            var key = BuildKey(name, location);
            index[key] = metadata;
        }

        cache.Set(ecoIndexCacheKey, index, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        });

        return index;
    }

    private static string? ResolveDatasetPath(string workspaceRoot)
    {
        foreach (var fileName in PreferredDatasetFiles)
        {
            var fullPath = Path.Combine(workspaceRoot, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private async Task<TrailOfflineEnrichmentItem?> TryGetSnapshotFromDatabaseAsync(int trailId, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await dbContext.TrailEnrichmentSnapshots
                .AsNoTracking()
                .Where(item => item.TrailId == trailId)
                .OrderByDescending(item => item.GeneratedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (snapshot is null)
            {
                return null;
            }

            if (snapshot.GeneratedAtUtc < DateTime.UtcNow - TrailCacheDuration)
            {
                return null;
            }

            return JsonSerializer.Deserialize<TrailOfflineEnrichmentItem>(snapshot.PayloadJson, SnapshotJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Skipping SQL snapshot read for trail {TrailId}", trailId);
            return null;
        }
    }

    private async Task SaveSnapshotAsync(TrailOfflineEnrichmentItem item, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(item, SnapshotJsonOptions);
            var snapshot = new TrailEnrichmentSnapshot
            {
                TrailId = item.TrailId,
                PayloadJson = payload,
                GeneratedAtUtc = DateTime.UtcNow,
                SourcePreviewFetchedAtUtc = item.SourcePreview?.FetchedAt,
            };

            dbContext.TrailEnrichmentSnapshots.Add(snapshot);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Skipping SQL snapshot write for trail {TrailId}", item.TrailId);
        }
    }

    private async Task<LiveWeatherAlertsSummary> GetLiveWeatherAlertsAsync(CancellationToken cancellationToken)
    {
        const string cacheKey = "trail-offline-enrichment:weather-alerts";
        if (cache.TryGetValue(cacheKey, out LiveWeatherAlertsSummary? cached) && cached is not null)
        {
            return cached;
        }

        var sourceUrl = configuration["OfflineEnrichment:WeatherAlertsSourceUrl"]
            ?? "https://www.meteo.bg/bg/forecasts/warnings";

        var summary = new LiveWeatherAlertsSummary
        {
            SourceName = "NIMH Warnings",
            SourceUrl = sourceUrl,
            FetchedAt = DateTime.UtcNow,
            IsOfficialSource = true,
            Alerts = ["Няма налични предупреждения в кеша."],
        };

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            var html = await client.GetStringAsync(sourceUrl, cancellationToken);

            var text = Regex.Replace(WebUtility.HtmlDecode(html), "<[^>]+>", " ");
            var alertMatches = AlertLineRegex().Matches(text)
                .Select(match => Regex.Replace(match.Value, "\\s+", " ").Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();

            if (alertMatches.Length == 0)
            {
                var normalizedText = text.ToLowerInvariant();
                summary.Alerts = normalizedText.Contains("няма") && normalizedText.Contains("опас")
                    ? ["Няма активни опасни метеорологични явления според източника."]
                    : ["Няма разпознати структурирани предупреждения в страницата."];
            }
            else
            {
                summary.Alerts = alertMatches;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch live weather alerts from {SourceUrl}", sourceUrl);
            summary.Alerts = ["Неуспешно извличане на live предупреждения от официалния източник."];
        }

        cache.Set(cacheKey, summary, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = AlertsCacheDuration
        });

        return summary;
    }

    private async Task<(TrailSourcePreview? Preview, bool Failed)> TryGetSourcePreviewAsync(
        string sourceUrl,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"trail-offline-enrichment:source-preview:{sourceUrl.Trim()}";
        if (cache.TryGetValue(cacheKey, out TrailSourcePreview? cached) && cached is not null)
        {
            return (cached, false);
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(8);

            using var response = await httpClient.GetAsync(sourceUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (null, true);
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var title = ExtractHtmlValue(TitleRegex(), html);
            var description = ExtractHtmlValue(DescriptionRegex(), html);

            var preview = new TrailSourcePreview
            {
                Title = title,
                Description = description,
                FetchedAt = DateTime.UtcNow,
            };

            cache.Set(cacheKey, preview, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SourcePreviewCacheDuration
            });

            return (preview, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Source preview scraping failed for {SourceUrl}", sourceUrl);
            return (null, true);
        }
    }

    private static TrailOfflineEnrichmentItem BuildItem(
        Trail trail,
        EcoTrailMetadata? metadata,
        TrailSourcePreview? sourcePreview,
        DateTime generatedAt)
    {
        var webcamLinks = BuildWebcamLinks(trail);

        return new TrailOfflineEnrichmentItem
        {
            TrailId = trail.Id,
            TrailName = trail.Name,
            Location = trail.Location,
            CachedAt = generatedAt,
            SourceUrl = metadata?.SourceUrl,
            SourcePreview = sourcePreview,
            Transport = new TrailTransportInfo
            {
                PublicTransport = metadata?.PublicTransport,
                ParkingAvailable = metadata?.ParkingAvailable,
            },
            Accessibility = new TrailAccessibilityInfo
            {
                WheelchairAccessible = metadata?.WheelchairAccessible,
                StrollerFriendly = metadata?.StrollerFriendly,
                BicycleAllowed = metadata?.BicycleAllowed,
            },
            SafetyWarnings = metadata?.SafetyWarnings ?? [],
            NearbyAmenities = metadata?.NearbyAmenities ?? [],
            EquipmentNeeded = metadata?.EquipmentNeeded ?? [],
            SuitabilityTags = metadata?.SuitabilityTags ?? [],
            BestMonths = metadata?.BestMonths ?? [],
            WinterAccessible = metadata?.WinterAccessible,
            WeatherDependent = metadata?.WeatherDependent,
            WebcamLinks = webcamLinks,
            AccessNotes = BuildAccessNotes(metadata),
        };
    }

    private static IReadOnlyList<string> BuildAccessNotes(EcoTrailMetadata? metadata)
    {
        List<string> notes = [];
        if (metadata is null)
        {
            notes.Add("Липсват структурирани данни за достъп в eco.json за тази пътека.");
            return notes;
        }

        if (!string.IsNullOrWhiteSpace(metadata.PublicTransport))
        {
            notes.Add($"Обществен транспорт: {metadata.PublicTransport}");
        }

        if (metadata.ParkingAvailable.HasValue)
        {
            notes.Add(metadata.ParkingAvailable.Value
                ? "Има наличен паркинг в близост."
                : "Няма потвърден паркинг в близост.");
        }

        if (metadata.WeatherDependent == true)
        {
            notes.Add("Маршрутът е силно зависим от времето - провери условията преди тръгване.");
        }

        return notes;
    }

    private static IReadOnlyList<string> BuildWebcamLinks(Trail trail)
    {
        var query = Uri.EscapeDataString($"{trail.Name} {trail.Location} webcam");
        List<string> links =
        [
            $"https://www.youtube.com/results?search_query={query}",
        ];

        if (trail.Latitude.HasValue && trail.Longitude.HasValue)
        {
            var lat = trail.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lng = trail.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            links.Add($"https://www.windy.com/{lat}/{lng}?webcams");
        }

        return links;
    }

    private static string BuildKey(string name, string location)
    {
        return $"{name.Trim()}|{location.Trim()}";
    }

    private static string ResolveLocation(string trailName, string nearestTown, string region)
    {
        if (!string.IsNullOrWhiteSpace(nearestTown))
        {
            return nearestTown;
        }

        if (!string.IsNullOrWhiteSpace(region) && !PlaceholderRegions.Contains(region.Trim()))
        {
            return region;
        }

        return !string.IsNullOrWhiteSpace(trailName) ? trailName.Trim() : "Неуточнено";
    }

    private static string? ExtractHtmlValue(Regex regex, string html)
    {
        var match = regex.Match(html);
        if (!match.Success || match.Groups.Count < 2)
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
        return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string GetNestedString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out var next))
            {
                return string.Empty;
            }

            current = next;
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? string.Empty : string.Empty;
    }

    private static bool? GetNestedBool(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind == JsonValueKind.True || current.ValueKind == JsonValueKind.False
            ? current.GetBoolean()
            : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static IReadOnlyList<string> GetNestedStringArray(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out var next))
            {
                return [];
            }

            current = next;
        }

        if (current.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return current
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private sealed class EcoTrailMetadata
    {
        public string? SourceUrl { get; set; }
        public IReadOnlyList<string> EquipmentNeeded { get; set; } = [];
        public IReadOnlyList<string> SafetyWarnings { get; set; } = [];
        public IReadOnlyList<string> NearbyAmenities { get; set; } = [];
        public IReadOnlyList<string> SuitabilityTags { get; set; } = [];
        public IReadOnlyList<string> BestMonths { get; set; } = [];
        public string? PublicTransport { get; set; }
        public bool? ParkingAvailable { get; set; }
        public bool? WheelchairAccessible { get; set; }
        public bool? StrollerFriendly { get; set; }
        public bool? BicycleAllowed { get; set; }
        public bool? WinterAccessible { get; set; }
        public bool? WeatherDependent { get; set; }
    }
}
