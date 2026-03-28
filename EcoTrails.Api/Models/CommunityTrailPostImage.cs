namespace EcoTrails.Api.Models;

public class CommunityTrailPostImage
{
    public int Id { get; set; }
    public int CommunityTrailPostId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public CommunityTrailPost? Post { get; set; }
}
