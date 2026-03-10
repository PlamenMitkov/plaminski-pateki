using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Tests;

public class AssistantRateLimitTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public AssistantRateLimitTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Enrich_WhenCalledMoreThanLimitWithinWindow_ReturnsTooManyRequests()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "assistant-admin-rate-limit");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var request = new AssistantEnrichRequest
        {
            Limit = 1,
            OverwriteExisting = false
        };

        var responses = new List<HttpResponseMessage>();
        for (var index = 0; index < 4; index++)
        {
            responses.Add(await client.PostAsJsonAsync("/api/assistant/enrich", request));
        }

        var rejected = responses.Last();
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out var retryAfterValues));

        var retryAfterValue = retryAfterValues!.Single();
        Assert.True(int.TryParse(retryAfterValue, out var retryAfterSeconds));
        Assert.True(retryAfterSeconds > 0);
    }
}
