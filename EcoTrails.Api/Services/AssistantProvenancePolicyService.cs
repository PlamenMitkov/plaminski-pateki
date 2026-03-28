using System.Text.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public sealed class AssistantProvenancePolicyService : IAssistantProvenancePolicyService
{
    private readonly AppDbContext _dbContext;
    private readonly OpenAiOptions _options;

    public AssistantProvenancePolicyService(AppDbContext dbContext, IOptions<OpenAiOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<AssistantProvenanceContextResult> BuildContextAsync(
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives,
        CancellationToken cancellationToken)
    {
        if (!_options.EnforceSourceProvenance)
        {
            foreach (var item in trails)
            {
                item.HasVerifiedSource = true;
            }

            foreach (var item in alternatives)
            {
                item.HasVerifiedSource = true;
            }

            return new AssistantProvenanceContextResult(trails, alternatives, false, null);
        }

        var allIds = trails.Select(item => item.Id)
            .Concat(alternatives.Select(item => item.Id))
            .Distinct()
            .ToList();

        if (allIds.Count == 0)
        {
            return new AssistantProvenanceContextResult(trails, alternatives, false, null);
        }

        var snapshotCandidates = await _dbContext.TrailEnrichmentSnapshots
            .AsNoTracking()
            .Where(item => allIds.Contains(item.TrailId) && item.SourcePreviewFetchedAtUtc.HasValue)
            .ToListAsync(cancellationToken);

        var latestSnapshots = snapshotCandidates
            .GroupBy(item => item.TrailId)
            .Select(group => group.OrderByDescending(item => item.GeneratedAtUtc).First())
            .ToList();

        var trustedDomains = _options.TrustedSourceDomainAllowList
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToLowerInvariant())
            .ToList();

        var quarantinedDomains = _options.QuarantinedSourceDomains
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToLowerInvariant())
            .ToHashSet();

        var verifiedIds = new HashSet<int>();
        var quarantinedIds = new HashSet<int>();

        foreach (var snapshot in latestSnapshots)
        {
            var host = ExtractSourceHostFromSnapshotPayload(snapshot.PayloadJson);
            if (string.IsNullOrWhiteSpace(host))
            {
                continue;
            }

            var normalizedHost = host.ToLowerInvariant();
            if (quarantinedDomains.Contains(normalizedHost))
            {
                quarantinedIds.Add(snapshot.TrailId);
                continue;
            }

            if (trustedDomains.Count == 0 || trustedDomains.Any(domain => SourceHostMatchesDomain(normalizedHost, domain)))
            {
                verifiedIds.Add(snapshot.TrailId);
            }
        }

        foreach (var item in trails)
        {
            item.HasVerifiedSource = verifiedIds.Contains(item.Id);
        }

        foreach (var item in alternatives)
        {
            item.HasVerifiedSource = verifiedIds.Contains(item.Id);
        }

        var quarantinedCount = trails.Count(item => quarantinedIds.Contains(item.Id));

        if (!_options.RequireVerifiedSourceForContext)
        {
            var hasWarning = trails.Any(item => !item.HasVerifiedSource);
            var note = hasWarning
                ? quarantinedCount > 0
                    ? $"Има {quarantinedCount} маршрута в quarantine по източник и маршрути без верифициран източник; интерпретирай детайлите с повишено внимание."
                    : "Има маршрути без верифициран източник; интерпретирай детайлите с повишено внимание."
                : null;
            return new AssistantProvenanceContextResult(trails, alternatives, hasWarning, note);
        }

        var filteredTrails = trails
            .Where(item => item.HasVerifiedSource && !quarantinedIds.Contains(item.Id))
            .ToList();
        var filteredAlternatives = alternatives
            .Where(item => item.HasVerifiedSource && !quarantinedIds.Contains(item.Id))
            .ToList();

        if (filteredTrails.Count == 0)
        {
            return new AssistantProvenanceContextResult(
                trails,
                alternatives,
                true,
                "Липсват верифицирани източници за текущите маршрути; използвай предпазлив език и не прави категорични фактически твърдения.");
        }

        var dropped = trails.Count - filteredTrails.Count;
        var reliabilityNote = dropped > 0
            ? quarantinedCount > 0
                ? $"Изключени са {dropped} маршрута с невалидиран или quarantined източник от контекста."
                : $"Изключени са {dropped} маршрута без верифициран източник от контекста."
            : null;

        return new AssistantProvenanceContextResult(filteredTrails, filteredAlternatives, dropped > 0, reliabilityNote);
    }

    private static string? ExtractSourceHostFromSnapshotPayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? sourceUrl = null;
            if (root.TryGetProperty("SourceUrl", out var sourceUrlProperty) && sourceUrlProperty.ValueKind == JsonValueKind.String)
            {
                sourceUrl = sourceUrlProperty.GetString();
            }
            else if (root.TryGetProperty("sourceUrl", out var sourceUrlCamelProperty) && sourceUrlCamelProperty.ValueKind == JsonValueKind.String)
            {
                sourceUrl = sourceUrlCamelProperty.GetString();
            }

            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                return null;
            }

            if (!Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out var sourceUri))
            {
                return null;
            }

            return sourceUri.Host;
        }
        catch
        {
            return null;
        }
    }

    private static bool SourceHostMatchesDomain(string host, string trustedDomain)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(trustedDomain))
        {
            return false;
        }

        var normalizedHost = host.Trim().ToLowerInvariant();
        var normalizedDomain = trustedDomain.Trim().ToLowerInvariant();

        return normalizedHost.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase)
               || normalizedHost.EndsWith($".{normalizedDomain}", StringComparison.OrdinalIgnoreCase);
    }
}
