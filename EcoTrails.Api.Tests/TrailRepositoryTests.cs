using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;

namespace EcoTrails.Api.Tests;

public class TrailRepositoryTests
{
    [Fact]
    public async Task GetPagedTrailsAsync_AppliesFiltersSortingAndPaging()
    {
        await using var context = TestDbContextFactory.CreateContext();
        context.Trails.RemoveRange(context.Trails);
        await context.SaveChangesAsync();

        var trails = new[]
        {
            new Trail { Id = 10, Name = "Rila Meadow", Location = "Rila", Description = "A", Difficulty = 2, DurationInHours = 2.0, ElevationGain = 120, Latitude = 42.1, Longitude = 23.5 },
            new Trail { Id = 11, Name = "Rila Peak", Location = "Rila", Description = "B", Difficulty = 3, DurationInHours = 4.0, ElevationGain = 500, Latitude = 42.2, Longitude = 23.6 },
            new Trail { Id = 12, Name = "Vitosha Walk", Location = "Sofia", Description = "C", Difficulty = 2, DurationInHours = 1.0, ElevationGain = 50 },
            new Trail { Id = 13, Name = "Rila Lake", Location = "Rila", Description = "D", Difficulty = 2, DurationInHours = 3.0, ElevationGain = 200, Latitude = 42.15, Longitude = 23.55 }
        };

        await context.Trails.AddRangeAsync(trails);
        await context.SaveChangesAsync();

        var repository = new TrailRepository(context);

        var result = await repository.GetPagedTrailsAsync(new TrailQueryParameters
        {
            Search = "Rila",
            Difficulty = 2,
            OnlyWithCoords = true,
            MinDuration = 2,
            SortBy = "duration",
            SortDirection = "desc",
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count());
        Assert.Equal(new[] { 13, 10 }, result.Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task ExportTrailsAsync_WithInvalidIds_ReturnsEmptyCollection()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var repository = new TrailRepository(context);

        var result = await repository.ExportTrailsAsync(
            new TrailQueryParameters { Search = "Rila" },
            ids: "x,y,z");

        Assert.Empty(result);
    }
}
