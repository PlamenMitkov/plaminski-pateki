using System.ComponentModel.DataAnnotations;

namespace EcoTrails.Api.Contracts;

public class FavoritesSyncRequest
{
    [Required]
    [MaxLength(2000)]
    public required List<int> TrailIds { get; set; } = [];
}