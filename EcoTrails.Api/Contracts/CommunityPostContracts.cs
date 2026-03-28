namespace EcoTrails.Api.Contracts;

public sealed class CommunityPostUpdateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? TrailId { get; set; }
    public string? PostType { get; set; }
}

public sealed class CommunityPostApproveRequest
{
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? Region { get; set; }
    public string? DifficultyLevel { get; set; }
    public double? DurationInHours { get; set; }
    public int? ElevationGain { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool? WaterSources { get; set; }
    public bool? SuitableForKids { get; set; }
    public int? MaxAltitude { get; set; }
    public string? RequiredGearJson { get; set; }
}

public sealed class CommunityPostRejectRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class CommunityPostAiReviewResponse
{
    public bool IsLikelyTrailProposal { get; set; }
    public int ReliabilityScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public string SuggestedLocation { get; set; } = string.Empty;
    public string SuggestedRegion { get; set; } = string.Empty;
    public string SuggestedDifficultyLevel { get; set; } = "Moderate";
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public sealed class CommunityPostResponse
{
    public int Id { get; set; }
    public int? TrailId { get; set; }
    public string TrailName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string PostType { get; set; } = "General";
    public string ProposalStatus { get; set; } = "None";
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyList<string> ImageUrls { get; set; } = [];
    public CommunityPostAiReviewResponse? AiReview { get; set; }
}
