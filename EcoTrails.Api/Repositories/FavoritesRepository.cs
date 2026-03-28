using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Repositories;

public class FavoritesRepository(AppDbContext context) : IFavoritesRepository
{
    public async Task<IReadOnlyList<int>> GetFavoriteTrailIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await context.UserFavoriteTrails
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.TrailId)
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> SyncFavoritesAsync(
        string userId,
        IEnumerable<int> requestedTrailIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequestedIds = requestedTrailIds
            .Where(item => item > 0)
            .Distinct()
            .ToHashSet();

        var validTrailIds = await context.Trails
            .AsNoTracking()
            .Where(item => normalizedRequestedIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        // Performance optimization: Using transaction to wrap ExecuteDeleteAsync and subsequent inserts
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.UserFavoriteTrails
            .Where(item => item.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        var newFavorites = validTrailIds
            .Select(trailId => new UserFavoriteTrail
            {
                UserId = userId,
                TrailId = trailId,
                CreatedAt = DateTime.UtcNow
            });

        await context.UserFavoriteTrails.AddRangeAsync(newFavorites, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return [.. validTrailIds.OrderBy(item => item)];
    }
}
