using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Services;

public class EcoJsonImportService
{
    private static readonly Regex NumberRegex = new(@"\d+(?:[\.,]\d+)?", RegexOptions.Compiled);
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EcoJsonImportService> _logger;

    public EcoJsonImportService(
        AppDbContext context,
        IWebHostEnvironment environment,
        ILogger<EcoJsonImportService> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task<int> ImportFromEcoJsonAsync(CancellationToken cancellationToken = default)
    {
        var workspaceRoot = Directory.GetParent(_environment.ContentRootPath)?.FullName;
        if (workspaceRoot is null)
        {
            return 0;
        }

        var jsonPath = Path.Combine(workspaceRoot, "eco.json");
        if (!File.Exists(jsonPath))
        {
            _logger.LogWarning("eco.json was not found at {Path}", jsonPath);
            return 0;
        }

        await using var stream = File.OpenRead(jsonPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("eco_trails", out var trailsElement) || trailsElement.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("eco.json does not contain a valid eco_trails array");
            return 0;
        }

        var existingTrails = await _context.Trails.ToListAsync(cancellationToken);
        var existingByKey = existingTrails
            .GroupBy(item => BuildKey(item.Name, item.Location), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var itemsToAdd = new List<Trail>();
        var updatedItems = 0;

        foreach (var trailElement in trailsElement.EnumerateArray())
        {
            var name = GetString(trailElement, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var description = GetString(trailElement, "description");
            var shortSummary = GetString(trailElement, "short_summary");
            var nearestTown = GetNestedString(trailElement, "location", "nearest_town");
            var region = GetNestedString(trailElement, "location", "region");
            var location = !string.IsNullOrWhiteSpace(nearestTown)
                ? nearestTown
                : (!string.IsNullOrWhiteSpace(region) ? region : "Неуточнено");
            var normalizedRegion = !string.IsNullOrWhiteSpace(region) ? region : location;
            var resolvedDescription = ResolveDescription(description, shortSummary, name, location);

            var difficultyText = GetNestedString(trailElement, "trail_details", "difficulty");
            var durationText = GetNestedString(trailElement, "trail_details", "duration");
            var equipmentNeeded = GetStringArray(trailElement, "equipment_needed");
            var suitability = GetStringArray(trailElement, "suitability");
            var nearbyAmenities = GetStringArray(trailElement, "nearby_amenities");
            var maxAltitudeText = GetNestedString(trailElement, "trail_details", "max_altitude_m");
            var lengthKmText = GetNestedString(trailElement, "trail_details", "length_km");
            var latitude = GetNestedDouble(trailElement, "location", "coordinates", "latitude");
            var longitude = GetNestedDouble(trailElement, "location", "coordinates", "longitude");
            var mappedDifficulty = MapDifficulty(difficultyText);
            var mappedMaxAltitude = ParseNullableInt(maxAltitudeText);
            var mappedElevationGain = ParseElevationGain(lengthKmText, mappedDifficulty);
            var mappedWaterSources = InferWaterSources(nearbyAmenities);
            var mappedSuitableForKids = suitability.Any(item =>
                item.Contains("family_with_kids", StringComparison.OrdinalIgnoreCase));
            var mappedRequiredGear = equipmentNeeded.Count > 0
                ? JsonSerializer.Serialize(equipmentNeeded)
                : "[\"туристически обувки\",\"вода\"]";

            var key = BuildKey(name, location);
            if (existingByKey.TryGetValue(key, out var existingTrail))
            {
                var changed = false;

                if (!existingTrail.Latitude.HasValue && latitude.HasValue)
                {
                    existingTrail.Latitude = latitude;
                    changed = true;
                }

                if (!existingTrail.Longitude.HasValue && longitude.HasValue)
                {
                    existingTrail.Longitude = longitude;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existingTrail.Region) && !string.IsNullOrWhiteSpace(normalizedRegion))
                {
                    existingTrail.Region = normalizedRegion;
                    changed = true;
                }

                if (!IsMissingDescription(resolvedDescription) &&
                    (IsMissingDescription(existingTrail.Description) ||
                     resolvedDescription.Length > existingTrail.Description.Length + 20))
                {
                    existingTrail.Description = resolvedDescription;
                    changed = true;
                }

                if (existingTrail.ElevationGain <= 0 && mappedElevationGain > 0)
                {
                    existingTrail.ElevationGain = mappedElevationGain;
                    changed = true;
                }

                if (!existingTrail.WaterSources && mappedWaterSources)
                {
                    existingTrail.WaterSources = true;
                    changed = true;
                }

                if (!existingTrail.SuitableForKids && mappedSuitableForKids)
                {
                    existingTrail.SuitableForKids = true;
                    changed = true;
                }

                if (existingTrail.MaxAltitude is null && mappedMaxAltitude.HasValue)
                {
                    existingTrail.MaxAltitude = mappedMaxAltitude.Value;
                    changed = true;
                }

                if ((string.IsNullOrWhiteSpace(existingTrail.RequiredGear) || existingTrail.RequiredGear == "[]") &&
                    mappedRequiredGear != "[]")
                {
                    existingTrail.RequiredGear = mappedRequiredGear;
                    changed = true;
                }

                if (changed)
                {
                    updatedItems++;
                }

                continue;
            }

            var newTrail = new Trail
            {
                Name = name,
                Description = resolvedDescription,
                Location = location,
                Region = normalizedRegion,
                Difficulty = mappedDifficulty,
                DifficultyLevel = MapDifficultyLevel(mappedDifficulty),
                WaterSources = false,
                MaxAltitude = mappedMaxAltitude,
                SuitableForKids = mappedSuitableForKids || mappedDifficulty <= 2,
                RequiredGear = mappedRequiredGear,
                DurationInHours = ParseDurationInHours(durationText),
                ElevationGain = mappedElevationGain,
                Latitude = latitude,
                Longitude = longitude,
                CreatedAt = DateTime.UtcNow
            };

            newTrail.WaterSources = mappedWaterSources;

            itemsToAdd.Add(newTrail);
            existingByKey[key] = newTrail;
        }

        if (itemsToAdd.Count == 0 && updatedItems == 0)
        {
            return 0;
        }

        if (itemsToAdd.Count > 0)
        {
            await _context.Trails.AddRangeAsync(itemsToAdd, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Imported {AddedCount} trails and updated coordinates for {UpdatedCount} trails from eco.json", itemsToAdd.Count, updatedItems);
        return itemsToAdd.Count + updatedItems;
    }

    private static string BuildKey(string name, string location)
    {
        return $"{name.Trim()}|{location.Trim()}";
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

    private static List<string> GetStringArray(JsonElement element, string propertyName)
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
            .Select(value => value!.Trim())
            .ToList();
    }

    private static double? GetNestedDouble(JsonElement element, params string[] path)
    {
        var value = GetNestedString(element, path);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int MapDifficulty(string difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return 3;
        }

        var normalized = difficulty.Trim().ToLowerInvariant();

        if (normalized.Contains("лека")) return 1;
        if (normalized.Contains("умерен")) return 2;
        if (normalized.Contains("средно") || normalized.Contains("средна")) return 3;
        if (normalized.Contains("трудна")) return 4;
        if (normalized.Contains("тежка") || normalized.Contains("екстрем")) return 5;

        return 3;
    }

    private static string ResolveDescription(string description, string shortSummary, string name, string location)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(shortSummary))
        {
            return shortSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(location) && !string.Equals(location, "Неуточнено", StringComparison.OrdinalIgnoreCase))
        {
            return $"Екопътека \"{name.Trim()}\" в района на {location.Trim()}. Описание предстои да бъде допълнено.";
        }

        return $"Екопътека \"{name.Trim()}\". Описание предстои да бъде допълнено.";
    }

    private static bool IsMissingDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return true;
        }

        var normalized = description.Trim();
        return normalized.Equals("Няма описание.", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Няма описание", StringComparison.OrdinalIgnoreCase);
    }

    private static bool InferWaterSources(IReadOnlyList<string> nearbyAmenities)
    {
        return nearbyAmenities.Any(item =>
            item.Contains("вода", StringComparison.OrdinalIgnoreCase) ||
            item.Contains("извор", StringComparison.OrdinalIgnoreCase));
    }

    private static int? ParseNullableInt(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var numbers = NumberRegex
            .Matches(rawValue)
            .Select(match => int.TryParse(match.Value, out var value) ? value : 0)
            .Where(value => value > 0)
            .ToList();

        if (numbers.Count == 0)
        {
            return null;
        }

        return numbers.Max();
    }

    private static int ParseElevationGain(string lengthKmText, int difficulty)
    {
        if (string.IsNullOrWhiteSpace(lengthKmText))
        {
            return difficulty switch
            {
                <= 2 => 180,
                >= 4 => 650,
                _ => 350,
            };
        }

        var normalized = lengthKmText.Trim().ToLowerInvariant();
        if (normalized.Contains("не е посоч") || normalized.Contains("варира") || normalized == "")
        {
            return difficulty switch
            {
                <= 2 => 180,
                >= 4 => 650,
                _ => 350,
            };
        }

        var numbers = NumberRegex
            .Matches(normalized)
            .Select(match => double.TryParse(match.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0)
            .Where(value => value > 0)
            .ToList();

        if (numbers.Count == 0)
        {
            return difficulty switch
            {
                <= 2 => 180,
                >= 4 => 650,
                _ => 350,
            };
        }

        var distanceKm = numbers.Count >= 2 && normalized.Contains('-')
            ? (numbers[0] + numbers[1]) / 2.0
            : numbers[0];

        var difficultyMultiplier = difficulty switch
        {
            <= 2 => 35,
            >= 4 => 75,
            _ => 55,
        };

        var estimated = (int)Math.Round(distanceKm * difficultyMultiplier, MidpointRounding.AwayFromZero);
        return Math.Clamp(estimated, 80, 1600);
    }

    private static TrailDifficultyLevel MapDifficultyLevel(int difficulty)
    {
        if (difficulty <= 2)
        {
            return TrailDifficultyLevel.Easy;
        }

        if (difficulty >= 4)
        {
            return TrailDifficultyLevel.Difficult;
        }

        return TrailDifficultyLevel.Moderate;
    }

    private static double ParseDurationInHours(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return 2.0;
        }

        var normalized = duration.Trim().ToLowerInvariant();
        if (normalized.Contains("варира") || normalized.Contains("не е посочена") || normalized.Contains("неизвест"))
        {
            return 2.0;
        }

        var numbers = NumberRegex
            .Matches(normalized)
            .Select(match => double.Parse(match.Value.Replace(',', '.'), CultureInfo.InvariantCulture))
            .ToList();

        if (numbers.Count == 0)
        {
            return 2.0;
        }

        var value = numbers.Count >= 2 && normalized.Contains('-')
            ? (numbers[0] + numbers[1]) / 2.0
            : numbers[0];

        if (normalized.Contains("мин"))
        {
            return Math.Round(value / 60.0, 2);
        }

        if (normalized.Contains("ден"))
        {
            return Math.Round(value * 8.0, 2);
        }

        return Math.Round(value, 2);
    }
}