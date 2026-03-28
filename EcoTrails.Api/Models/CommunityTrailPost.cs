namespace EcoTrails.Api.Models;

public enum CommunityPostType
{
    General = 0,
    TrailFeedback = 1,
    TrailProposal = 2,
}

public enum ProposalStatus
{
    None = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public class CommunityTrailPost
{
    public int Id { get; set; }
    public string AppUserId { get; set; } = string.Empty;
    public int? TrailId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public CommunityPostType PostType { get; set; } = CommunityPostType.General;
    public ProposalStatus ProposalStatus { get; set; } = ProposalStatus.None;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
    public Trail? Trail { get; set; }
    public ICollection<CommunityTrailPostImage> Images { get; set; } = new List<CommunityTrailPostImage>();
}
