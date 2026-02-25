namespace EcoTrails.Api.Models;

public class UserFavoriteTrail
{
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public int TrailId { get; set; }
    public Trail Trail { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}