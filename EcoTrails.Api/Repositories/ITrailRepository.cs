using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;

namespace EcoTrails.Api.Repositories;

public interface ITrailRepository
{
    Task<PagedResponse<Trail>> GetPagedTrailsAsync(TrailQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Trail>> ExportTrailsAsync(
        TrailQueryParameters parameters,
        string? ids,
        CancellationToken cancellationToken = default);
    Task<Trail?> GetTrailByIdAsync(int id, bool asNoTracking = true, CancellationToken cancellationToken = default);
    Task AddTrailAsync(Trail trail, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
