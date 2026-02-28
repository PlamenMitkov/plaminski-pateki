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
    public string DifficultyLevel { get; set; } = "moderate";
    public bool WaterSources { get; set; }
    public int? MaxAltitude { get; set; }
    public bool SuitableForKids { get; set; }
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
    public string Prompt { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public List<AssistantChatMessage> History { get; set; } = [];
    public string? FilterSummary { get; set; }
    public int FavoriteCount { get; set; }
    public List<int> FavoriteTrailIds { get; set; } = [];
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
    public int? Limit { get; set; }
    public bool OverwriteExisting { get; set; }
    public List<int>? TrailIds { get; set; }
}

public class AssistantEnrichResponse
{
    public int Processed { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = [];
}