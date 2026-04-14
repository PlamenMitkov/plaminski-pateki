namespace EcoTrails.Api.Services;

public sealed class OpenAiOptions
{
    public string Provider { get; set; } = "gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string OpenAiFallbackModel { get; set; } = "gpt-4o-mini";
    public string GeminiApiKey { get; set; } = string.Empty;
    public string GeminiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string GeminiModel { get; set; } = "gemini-2.5-flash";

    public bool FallbackToOpenAiOnGemini429 { get; set; } = true;
    public bool FallbackToOpenAiOnGeminiUnavailable { get; set; } = true;

    public string AssistantMode { get; set; } = "current";
    public bool PromptTemplateShadowMode { get; set; }
    public bool PromptTemplateFailOpen { get; set; } = true;
    public bool PromptInjectionGuardEnabled { get; set; } = true;
    public bool PromptInjectionBlockOnDetect { get; set; }

    public bool EnforceSourceProvenance { get; set; } = true;
    public bool RequireVerifiedSourceForContext { get; set; } = true;
    public List<string> TrustedSourceDomainAllowList { get; set; } = [];
    public List<string> QuarantinedSourceDomains { get; set; } = [];

    public bool WeatherEnabled { get; set; } = true;
    public string WeatherApiBaseUrl { get; set; } = "https://api.open-meteo.com/v1";
    public string WeatherGeocodingBaseUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1";

    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
    public int EnrichDelayMs { get; set; } = 200;

    public int RetryAttempts { get; set; } = 3;
    public int RetryInitialDelayMs { get; set; } = 500;
    public int RetryJitterMs { get; set; } = 200;

    public int RrfK { get; set; } = 60;
    public int VectorMultiplier { get; set; } = 2;
    public int TopK { get; set; } = 5;

    public int EmbeddingBatchSize { get; set; } = 10;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}
