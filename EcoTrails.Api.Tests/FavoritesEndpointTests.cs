using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Tests;

public class FavoritesEndpointTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public FavoritesEndpointTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SyncThenGetFavorites_ReturnsOnlyValidSortedTrailIds()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "favorites-user-1");

        var trailsResponse = await client.GetAsync("/api/trails?page=1&pageSize=20&sortBy=name&sortDirection=asc");
        Assert.Equal(HttpStatusCode.OK, trailsResponse.StatusCode);

        var trailsPayload = await trailsResponse.Content.ReadFromJsonAsync<EcoTrails.Api.Models.PagedResponse<EcoTrails.Api.Models.Trail>>();
        Assert.NotNull(trailsPayload);

        var vitoshaId = trailsPayload!.Items.First(item => item.Name == "Vitosha Ring").Id;
        var rilaId = trailsPayload.Items.First(item => item.Name == "Rila Panorama").Id;

        var syncResponse = await client.PostAsJsonAsync("/api/favorites/sync", new FavoritesSyncRequest
        {
            TrailIds = [999999, -5, rilaId, vitoshaId, vitoshaId]
        });

        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);

        var synced = await syncResponse.Content.ReadFromJsonAsync<List<int>>();
        Assert.NotNull(synced);
        Assert.Equal(new[] { rilaId, vitoshaId }.Order().ToArray(), synced!.ToArray());

        var getResponse = await client.GetAsync("/api/favorites");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var favorites = await getResponse.Content.ReadFromJsonAsync<List<int>>();
        Assert.NotNull(favorites);
        Assert.Equal(new[] { rilaId, vitoshaId }.Order().ToArray(), favorites!.ToArray());
    }

    [Fact]
    public async Task GetFavorites_IsolatedPerUser()
    {
        using var firstUserClient = _factory.CreateClient();
        firstUserClient.DefaultRequestHeaders.Add("X-Test-UserId", "favorites-user-2");

        using var secondUserClient = _factory.CreateClient();
        secondUserClient.DefaultRequestHeaders.Add("X-Test-UserId", "favorites-user-3");

        var trailsResponse = await firstUserClient.GetAsync("/api/trails?page=1&pageSize=20");
        var trailsPayload = await trailsResponse.Content.ReadFromJsonAsync<EcoTrails.Api.Models.PagedResponse<EcoTrails.Api.Models.Trail>>();
        Assert.NotNull(trailsPayload);

        var oneTrailId = trailsPayload!.Items.First().Id;

        var syncFirstUser = await firstUserClient.PostAsJsonAsync("/api/favorites/sync", new FavoritesSyncRequest
        {
            TrailIds = [oneTrailId]
        });
        Assert.Equal(HttpStatusCode.OK, syncFirstUser.StatusCode);

        var firstUserFavoritesResponse = await firstUserClient.GetAsync("/api/favorites");
        var firstUserFavorites = await firstUserFavoritesResponse.Content.ReadFromJsonAsync<List<int>>();

        var secondUserFavoritesResponse = await secondUserClient.GetAsync("/api/favorites");
        var secondUserFavorites = await secondUserFavoritesResponse.Content.ReadFromJsonAsync<List<int>>();

        Assert.NotNull(firstUserFavorites);
        Assert.Single(firstUserFavorites!);
        Assert.Equal(oneTrailId, firstUserFavorites[0]);

        Assert.NotNull(secondUserFavorites);
        Assert.Empty(secondUserFavorites!);
    }

    [Fact]
    public async Task SyncFavorites_WithEmptyList_ClearsExistingFavorites()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "favorites-user-4");

        var trailsResponse = await client.GetAsync("/api/trails?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, trailsResponse.StatusCode);

        var trailsPayload = await trailsResponse.Content.ReadFromJsonAsync<EcoTrails.Api.Models.PagedResponse<EcoTrails.Api.Models.Trail>>();
        Assert.NotNull(trailsPayload);

        var oneTrailId = trailsPayload!.Items.First().Id;

        var seedSyncResponse = await client.PostAsJsonAsync("/api/favorites/sync", new FavoritesSyncRequest
        {
            TrailIds = [oneTrailId]
        });
        Assert.Equal(HttpStatusCode.OK, seedSyncResponse.StatusCode);

        var clearSyncResponse = await client.PostAsJsonAsync("/api/favorites/sync", new FavoritesSyncRequest
        {
            TrailIds = []
        });
        Assert.Equal(HttpStatusCode.OK, clearSyncResponse.StatusCode);

        var clearSyncPayload = await clearSyncResponse.Content.ReadFromJsonAsync<List<int>>();
        Assert.NotNull(clearSyncPayload);
        Assert.Empty(clearSyncPayload!);

        var getResponse = await client.GetAsync("/api/favorites");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var favorites = await getResponse.Content.ReadFromJsonAsync<List<int>>();
        Assert.NotNull(favorites);
        Assert.Empty(favorites!);
    }

    [Fact]
    public async Task SyncFavorites_WithoutAuthHeader_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/favorites/sync", new FavoritesSyncRequest
        {
            TrailIds = [1]
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFavorites_WithoutAuthHeader_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/favorites");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
