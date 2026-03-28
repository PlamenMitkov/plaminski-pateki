using System.Text.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public sealed class AssistantEnrichmentWorkflowService : IAssistantEnrichmentWorkflowService
{
    private readonly AppDbContext _dbContext;
    private readonly OpenAiOptions _options;
    private readonly ILogger<AssistantEnrichmentWorkflowService> _logger;

    public AssistantEnrichmentWorkflowService(
        AppDbContext dbContext,
        IOptions<OpenAiOptions> options,
        ILogger<AssistantEnrichmentWorkflowService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AssistantEnrichResponse> ExecuteAsync(
        AssistantEnrichRequest request,
        Func<Trail, CancellationToken, Task<AssistantTrailSemanticData>> extractSemanticDataAsync,
        CancellationToken cancellationToken)
    {
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
                var extracted = await extractSemanticDataAsync(trail, cancellationToken);
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

    private static bool HasSemanticData(Trail trail)
    {
        var hasGear = ParseRequiredGear(trail.RequiredGear).Count > 0;
        var hasDefaultDifficulty = trail.DifficultyLevel == TrailDifficultyLevel.Moderate;

        return hasGear ||
               trail.MaxAltitude.HasValue ||
               trail.WaterSources ||
               trail.SuitableForKids ||
               !hasDefaultDifficulty;
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
}
