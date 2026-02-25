namespace EcoTrails.Api.Models
{
    public class Trail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public int Difficulty { get; set; }

        public double DurationInHours { get; set; }

        public int ElevationGain { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserFavoriteTrail> FavoritedByUsers { get; set; } = new List<UserFavoriteTrail>();
    }
}