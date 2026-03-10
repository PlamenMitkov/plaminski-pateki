using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Repositories;

public class FavoritesRepository : IFavoritesRepository
{
    private readonly AppDbContext _context;

    public FavoritesRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<int>> GetFavoriteTrailIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserFavoriteTrails
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

        var validTrailIds = await _context.Trails
            .AsNoTracking()
            .Where(item => normalizedRequestedIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var existingFavorites = await _context.UserFavoriteTrails
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);

        _context.UserFavoriteTrails.RemoveRange(existingFavorites);

        var newFavorites = validTrailIds
            .Select(trailId => new UserFavoriteTrail
            {
                UserId = userId,
                TrailId = trailId,
                CreatedAt = DateTime.UtcNow
            });

        await _context.UserFavoriteTrails.AddRangeAsync(newFavorites, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return validTrailIds.OrderBy(item => item).ToList();
    }
}
