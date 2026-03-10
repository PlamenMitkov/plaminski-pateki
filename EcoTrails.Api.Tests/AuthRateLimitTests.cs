using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Tests;

public class AuthRateLimitTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public AuthRateLimitTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WhenCalledMoreThanAuthLimitWithinWindow_ReturnsTooManyRequests()
    {
        using var client = _factory.CreateClient();

        var request = new LoginRequest("nobody@example.com", "invalid1");
        var responses = new List<HttpResponseMessage>();

        for (var index = 0; index < 11; index++)
        {
            responses.Add(await client.PostAsJsonAsync("/api/auth/login", request));
        }

        var rejected = responses.Last();
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out var retryAfterValues));

        var retryAfterValue = retryAfterValues!.Single();
        Assert.True(int.TryParse(retryAfterValue, out var retryAfterSeconds));
        Assert.True(retryAfterSeconds > 0);
    }
}
