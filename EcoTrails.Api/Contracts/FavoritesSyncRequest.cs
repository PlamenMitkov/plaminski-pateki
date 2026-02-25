namespace EcoTrails.Api.Contracts;

public class FavoritesSyncRequest
{
    public List<int> TrailIds { get; set; } = new();
}