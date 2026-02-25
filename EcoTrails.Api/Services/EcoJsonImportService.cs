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
            var nearestTown = GetNestedString(trailElement, "location", "nearest_town");
            var region = GetNestedString(trailElement, "location", "region");
            var location = !string.IsNullOrWhiteSpace(nearestTown)
                ? nearestTown
                : (!string.IsNullOrWhiteSpace(region) ? region : "Неуточнено");

            var difficultyText = GetNestedString(trailElement, "trail_details", "difficulty");
            var durationText = GetNestedString(trailElement, "trail_details", "duration");
            var latitude = GetNestedDouble(trailElement, "location", "coordinates", "latitude");
            var longitude = GetNestedDouble(trailElement, "location", "coordinates", "longitude");

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

                if (changed)
                {
                    updatedItems++;
                }

                continue;
            }

            var newTrail = new Trail
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(description) ? "Няма описание." : description,
                Location = location,
                Difficulty = MapDifficulty(difficultyText),
                DurationInHours = ParseDurationInHours(durationText),
                ElevationGain = 0,
                Latitude = latitude,
                Longitude = longitude,
                CreatedAt = DateTime.UtcNow
            };

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