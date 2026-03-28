using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public sealed class AiProviderFallbackPolicy : IAiProviderFallbackPolicy
{
    private readonly OpenAiOptions _options;

    public AiProviderFallbackPolicy(IOptions<OpenAiOptions> options)
    {
        _options = options.Value;
    }

    public string? ResolveOpenAiFallbackModel(string primaryModel)
    {
        var configured = string.IsNullOrWhiteSpace(_options.OpenAiFallbackModel)
            ? "gpt-4o-mini"
            : _options.OpenAiFallbackModel.Trim();

        return string.Equals(primaryModel, configured, StringComparison.OrdinalIgnoreCase)
            ? null
            : configured;
    }

    public bool ShouldFallbackToSecondaryOpenAiModel(string primaryModel, AiProviderException exception)
    {
        return exception.StatusCode == StatusCodes.Status404NotFound &&
               HasOpenAiApiKey() &&
               !string.IsNullOrWhiteSpace(ResolveOpenAiFallbackModel(primaryModel));
    }

    public bool ShouldFallbackToOpenAiFromGemini(AiProviderException exception)
    {
        if (!HasOpenAiApiKey())
        {
            return false;
        }

        if (exception.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            return _options.FallbackToOpenAiOnGemini429;
        }

        return _options.FallbackToOpenAiOnGeminiUnavailable &&
               (exception.StatusCode == StatusCodes.Status404NotFound ||
                exception.StatusCode == StatusCodes.Status503ServiceUnavailable ||
                exception.StatusCode >= StatusCodes.Status500InternalServerError);
    }

    private bool HasOpenAiApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    }
}
