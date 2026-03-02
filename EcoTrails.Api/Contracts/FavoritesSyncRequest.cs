using System.ComponentModel.DataAnnotations;

namespace EcoTrails.Api.Contracts;

public class FavoritesSyncRequest
{
    [Required]
    [MaxLength(2000)]
    public List<int> TrailIds { get; set; } = new();
}