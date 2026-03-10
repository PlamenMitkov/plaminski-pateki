using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Tests;

public class FavoritesRepositoryTests
{
    [Fact]
    public async Task GetFavoriteTrailIdsAsync_ReturnsSortedIdsForUser()
    {
        await using var context = TestDbContextFactory.CreateContext();

        await context.UserFavoriteTrails.AddRangeAsync(
            new UserFavoriteTrail { UserId = "user-1", TrailId = 5 },
            new UserFavoriteTrail { UserId = "user-1", TrailId = 2 },
            new UserFavoriteTrail { UserId = "user-2", TrailId = 1 });
        await context.SaveChangesAsync();

        var repository = new FavoritesRepository(context);

        var result = await repository.GetFavoriteTrailIdsAsync("user-1");

        Assert.Equal(new[] { 2, 5 }, result.ToArray());
    }

    [Fact]
    public async Task SyncFavoritesAsync_ReplacesExistingFavorites_WithValidDistinctIdsOnly()
    {
        await using var context = TestDbContextFactory.CreateContext();

        await context.Trails.AddRangeAsync(
            new Trail { Id = 1, Name = "Trail 1", Location = "A", Description = "A", Difficulty = 1, DurationInHours = 1, ElevationGain = 10 },
            new Trail { Id = 2, Name = "Trail 2", Location = "B", Description = "B", Difficulty = 2, DurationInHours = 2, ElevationGain = 20 },
            new Trail { Id = 3, Name = "Trail 3", Location = "C", Description = "C", Difficulty = 3, DurationInHours = 3, ElevationGain = 30 });

        await context.UserFavoriteTrails.AddRangeAsync(
            new UserFavoriteTrail { UserId = "user-1", TrailId = 99 },
            new UserFavoriteTrail { UserId = "user-1", TrailId = 2 });

        await context.SaveChangesAsync();

        var repository = new FavoritesRepository(context);

        var synced = await repository.SyncFavoritesAsync("user-1", new[] { -1, 2, 2, 3, 5000 });

        Assert.Equal(new[] { 2, 3 }, synced.ToArray());

        var stored = await context.UserFavoriteTrails
            .Where(item => item.UserId == "user-1")
            .OrderBy(item => item.TrailId)
            .Select(item => item.TrailId)
            .ToListAsync();

        Assert.Equal(new[] { 2, 3 }, stored.ToArray());
    }
}
