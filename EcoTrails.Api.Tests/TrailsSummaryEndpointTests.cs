using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;

namespace EcoTrails.Api.Tests;

public class TrailsSummaryEndpointTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public TrailsSummaryEndpointTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSummary_ReturnsPagedViewModel_WithFormattedDescription()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/trails/summary?search=Rila&page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<TrailIndexViewModel>>();

        Assert.NotNull(payload);
        Assert.True(payload!.TotalCount >= 1);
        Assert.Single(payload.Items);

        var item = payload.Items.Single();
        Assert.Equal("Rila Panorama", item.Title);
        Assert.Equal("Moderate", item.Difficulty);
        Assert.Equal("Blagoevgrad", item.Region);
        Assert.EndsWith("...", item.ShortDescription);
        Assert.True(item.ShortDescription.Length <= 103);
    }
}
