using System.Text.Json.Serialization;
using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

[JsonSerializable(typeof(OpenAiPayload))]
[JsonSerializable(typeof(GeminiPayload))]
[JsonSerializable(typeof(AssistantChatMessage))]
[JsonSerializable(typeof(List<AssistantChatMessage>))]
[JsonSerializable(typeof(OpenAiMessage))]
[JsonSerializable(typeof(GeminiContent))]
[JsonSerializable(typeof(GeminiPart))]
internal partial class AiJsonContext : JsonSerializerContext
{
}

public class OpenAiPayload
{
    public string model { get; set; } = string.Empty;
    public double temperature { get; set; }
    public int max_tokens { get; set; }
    public object? response_format { get; set; }
    public List<OpenAiMessage> messages { get; set; } = [];
}

public class OpenAiMessage
{
    public string role { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}

public class GeminiPayload
{
    public List<GeminiContent> contents { get; set; } = [];
    public object generationConfig { get; set; } = new { };
}

public class GeminiContent
{
    public string role { get; set; } = string.Empty;
    public List<GeminiPart> parts { get; set; } = [];
}

public class GeminiPart
{
    public string text { get; set; } = string.Empty;
}
