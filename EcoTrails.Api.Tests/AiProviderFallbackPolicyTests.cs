using EcoTrails.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Tests;

public class AiProviderFallbackPolicyTests
{
    [Fact]
    public void ResolveOpenAiFallbackModel_ReturnsNullWhenPrimaryMatchesConfiguredFallback()
    {
        var policy = CreatePolicy(new OpenAiOptions
        {
            ApiKey = "openai-key",
            OpenAiFallbackModel = "gpt-4o-mini"
        });

        var resolved = policy.ResolveOpenAiFallbackModel("gpt-4o-mini");

        Assert.Null(resolved);
    }

    [Fact]
    public void ShouldFallbackToSecondaryOpenAiModel_ReturnsTrueOnModelNotFoundWithConfiguredKey()
    {
        var policy = CreatePolicy(new OpenAiOptions
        {
            ApiKey = "openai-key",
            OpenAiFallbackModel = "gpt-4o"
        });

        var result = policy.ShouldFallbackToSecondaryOpenAiModel(
            "gpt-4o-mini",
            new AiProviderException(StatusCodes.Status404NotFound, "model not found"));

        Assert.True(result);
    }

    [Fact]
    public void ShouldFallbackToOpenAiFromGemini_ReturnsTrueOn503WhenFeatureEnabledAndOpenAiKeyExists()
    {
        var policy = CreatePolicy(new OpenAiOptions
        {
            ApiKey = "openai-key",
            FallbackToOpenAiOnGeminiUnavailable = true
        });

        var result = policy.ShouldFallbackToOpenAiFromGemini(
            new AiProviderException(StatusCodes.Status503ServiceUnavailable, "gemini unavailable"));

        Assert.True(result);
    }

    [Fact]
    public void ShouldFallbackToOpenAiFromGemini_ReturnsFalseWhenOpenAiKeyMissing()
    {
        var originalOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        try
        {
            var policy = CreatePolicy(new OpenAiOptions
            {
                ApiKey = string.Empty,
                FallbackToOpenAiOnGeminiUnavailable = true,
                FallbackToOpenAiOnGemini429 = true
            });

            var result = policy.ShouldFallbackToOpenAiFromGemini(
                new AiProviderException(StatusCodes.Status429TooManyRequests, "rate limited"));

            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalOpenAiApiKey);
        }
    }

    private static AiProviderFallbackPolicy CreatePolicy(OpenAiOptions options)
        => new(Options.Create(options));
}
