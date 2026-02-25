using Microsoft.AspNetCore.Identity;

namespace EcoTrails.Api.Models;

public class AppUser : IdentityUser
{
    public ICollection<UserFavoriteTrail> FavoriteTrails { get; set; } = new List<UserFavoriteTrail>();
}