using System.ComponentModel.DataAnnotations;

namespace EcoTrails.Api.Contracts;

public class AssistantTrailContext
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int Difficulty { get; set; }
    public double DurationInHours { get; set; }
    public int ElevationGain { get; set; }
    public bool HasCoordinates { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string DifficultyLevel { get; set; } = "moderate";
    public bool WaterSources { get; set; }
    public int? MaxAltitude { get; set; }
    public bool SuitableForKids { get; set; }
    public bool HasVerifiedSource { get; set; }
    public List<string> RequiredGear { get; set; } = [];
}

public class AssistantChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class AssistantSessionCreateRequest
{
    public string? Title { get; set; }
}

public class AssistantSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public int MessageCount { get; set; }
    public bool IsOwnedByUser { get; set; }
}

public class AssistantSessionMessageResponse
{
    public int Id { get; set; }
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AssistantFeedbackRequest
{
    [Required]
    [MaxLength(64)]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string MessageId { get; set; } = string.Empty;

    public bool IsPositive { get; set; }
}

public class AssistantFeedbackResponse
{
    public bool Recorded { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}

public class AssistantKnowledgeChip
{
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "info";
}

public class AssistantQuickAction
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class AssistantChatRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(2000)]
    public string Prompt { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? SessionId { get; set; }

    [MaxLength(40)]
    public List<AssistantChatMessage> History { get; set; } = [];

    [MaxLength(500)]
    public string? FilterSummary { get; set; }

    [Range(0, 5000)]
    public int FavoriteCount { get; set; }

    [MaxLength(200)]
    public List<int> FavoriteTrailIds { get; set; } = [];

    [Range(1, 25)]
    public int MaxContextTrails { get; set; } = 15;
    public bool OnlyWithCoordinates { get; set; }
}

public class AssistantChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Reply { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = "openai";
    public List<AssistantTrailContext> UsedTrails { get; set; } = [];
    public List<AssistantTrailContext> SuggestedAlternatives { get; set; } = [];
    public List<int> SuggestedAlternativeIds { get; set; } = [];
    public List<AssistantKnowledgeChip> KnowledgeChips { get; set; } = [];
    public List<AssistantQuickAction> QuickActions { get; set; } = [];
}

public class AssistantEnrichRequest
{
    [Range(1, 300)]
    public int? Limit { get; set; }
    public bool OverwriteExisting { get; set; }

    [MaxLength(300)]
    public List<int>? TrailIds { get; set; }
}

public class AssistantEnrichResponse
{
    public int Processed { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class AssistantVectorIndexRequest
{
    [Range(1, 500)]
    public int? Limit { get; set; }

    public bool OverwriteExisting { get; set; }

    [MaxLength(500)]
    public List<int>? TrailIds { get; set; }
}

public class AssistantVectorIndexResponse
{
    public int Processed { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class AssistantVectorSearchRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(1000)]
    public string Prompt { get; set; } = string.Empty;

    [Range(1, 10)]
    public int TopK { get; set; } = 5;

    public bool OnlyWithCoordinates { get; set; }
}

public class AssistantVectorMatch
{
    public AssistantTrailContext Trail { get; set; } = new();
    public double Score { get; set; }
}

public class AssistantVectorSearchResponse
{
    public string Prompt { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<AssistantVectorMatch> Matches { get; set; } = [];
}

public class AssistantVectorIndexStartResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public string Message { get; set; } = "Vector indexing has been queued.";
}

public class AssistantVectorIndexJobStatusResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Processed { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = [];
}