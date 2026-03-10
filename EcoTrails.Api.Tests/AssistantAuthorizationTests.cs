using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Tests;

public class AssistantAuthorizationTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public AssistantAuthorizationTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Enrich_WithAuthenticatedNonAdminUser_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "assistant-user-1");

        var response = await client.PostAsJsonAsync("/api/assistant/enrich", new AssistantEnrichRequest
        {
            Limit = 1,
            OverwriteExisting = false
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enrich_WithAdminRole_PassesAuthorization()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "assistant-admin-1");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.PostAsJsonAsync("/api/assistant/enrich", new AssistantEnrichRequest
        {
            Limit = 1,
            OverwriteExisting = false
        });

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
