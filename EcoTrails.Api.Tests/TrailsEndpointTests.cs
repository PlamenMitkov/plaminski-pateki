using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Models;

namespace EcoTrails.Api.Tests;

public class TrailsEndpointTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public TrailsEndpointTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTrails_ReturnsPagingPayload_AndTotalCountHeader()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/trails?search=Rila&page=1&pageSize=1&sortBy=name&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Total-Count", out var totalCountValues));

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<Trail>>();

        Assert.NotNull(payload);
        Assert.Equal(payload!.TotalCount.ToString(), totalCountValues!.Single());
        Assert.True(payload.TotalCount >= 1);
        Assert.Equal(1, payload.Page);
        Assert.Equal(1, payload.PageSize);
        Assert.Single(payload.Items);
        Assert.Equal("Rila Panorama", payload.Items.Single().Name);
    }

    [Fact]
    public async Task GetTrails_ReturnsEtag_AndSupportsNotModified()
    {
        using var client = _factory.CreateClient();

        const string requestUri = "/api/trails?page=1&pageSize=2&sortBy=name&sortDirection=asc";
        var firstResponse = await client.GetAsync(requestUri);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.True(firstResponse.Headers.TryGetValues("ETag", out var etagValues));
        Assert.True(firstResponse.Headers.TryGetValues("Cache-Control", out var cacheControlValues));

        var etag = etagValues!.Single();
        Assert.Contains("max-age=60", cacheControlValues!.Single());

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
        secondRequest.Headers.TryAddWithoutValidation("If-None-Match", etag);

        var secondResponse = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.NotModified, secondResponse.StatusCode);
    }

    [Fact]
    public async Task ExportTrails_FiltersByIds_AndAppliesSorting()
    {
        using var client = _factory.CreateClient();

        var listResponse = await client.GetAsync("/api/trails?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponse<Trail>>();
        Assert.NotNull(listPayload);

        var rila = listPayload!.Items.First(item => item.Name == "Rila Panorama");
        var vitosha = listPayload.Items.First(item => item.Name == "Vitosha Ring");

        var exportResponse = await client.GetAsync(
            $"/api/trails/export?ids={rila.Id},{vitosha.Id}&sortBy=name&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);

        var exported = await exportResponse.Content.ReadFromJsonAsync<List<Trail>>();
        Assert.NotNull(exported);
        Assert.Equal(2, exported!.Count);
        Assert.Equal(new[] { "Vitosha Ring", "Rila Panorama" }, exported.Select(item => item.Name).ToArray());
    }

    [Fact]
    public async Task ExportTrails_WithInvalidIds_ReturnsEmptyArray()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/trails/export?ids=abc,0,-5,xyz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var exported = await response.Content.ReadFromJsonAsync<List<Trail>>();
        Assert.NotNull(exported);
        Assert.Empty(exported!);
    }

    [Fact]
    public async Task ExportTrails_WithMixedIds_ReturnsOnlyValidTrails()
    {
        using var client = _factory.CreateClient();

        var listResponse = await client.GetAsync("/api/trails?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<PagedResponse<Trail>>();
        Assert.NotNull(listPayload);

        var rila = listPayload!.Items.First(item => item.Name == "Rila Panorama");

        var response = await client.GetAsync($"/api/trails/export?ids=abc,-1,{rila.Id},999999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var exported = await response.Content.ReadFromJsonAsync<List<Trail>>();
        Assert.NotNull(exported);
        Assert.Single(exported!);
        Assert.Equal("Rila Panorama", exported[0].Name);
    }
}
