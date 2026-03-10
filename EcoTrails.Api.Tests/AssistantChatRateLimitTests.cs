using System.Net;
using System.Net.Http.Json;
using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Tests;

public class AssistantChatRateLimitTests : IClassFixture<TrailsSummaryApiFactory>
{
    private readonly TrailsSummaryApiFactory _factory;

    public AssistantChatRateLimitTests(TrailsSummaryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Chat_WhenCalledMoreThanTokenBucketLimit_ReturnsTooManyRequests()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "assistant-chat-rate-user");

        var request = new AssistantChatRequest
        {
            Prompt = "Препоръчай ми маршрут",
            MaxContextTrails = 5
        };

        var responses = new List<HttpResponseMessage>();
        for (var index = 0; index < 31; index++)
        {
            responses.Add(await client.PostAsJsonAsync("/api/assistant/chat", request));
        }

        var rejected = responses.Last();
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out var retryAfterValues));

        var retryAfterValue = retryAfterValues!.Single();
        Assert.True(int.TryParse(retryAfterValue, out var retryAfterSeconds));
        Assert.True(retryAfterSeconds > 0);
    }
}
