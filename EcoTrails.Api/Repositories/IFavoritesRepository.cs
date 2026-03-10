namespace EcoTrails.Api.Repositories;

public interface IFavoritesRepository
{
    Task<IReadOnlyList<int>> GetFavoriteTrailIdsAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> SyncFavoritesAsync(string userId, IEnumerable<int> requestedTrailIds, CancellationToken cancellationToken = default);
}
