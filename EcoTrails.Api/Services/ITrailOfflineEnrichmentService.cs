using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;

namespace EcoTrails.Api.Services;

public interface ITrailOfflineEnrichmentService
{
    Task<TrailOfflineEnrichmentResponse> GetOfflineEnrichmentAsync(
        IReadOnlyList<Trail> trails,
        CancellationToken cancellationToken = default);

    Task WarmDailyCacheAsync(CancellationToken cancellationToken = default);
}
