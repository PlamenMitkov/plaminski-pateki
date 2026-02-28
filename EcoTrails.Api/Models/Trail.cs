namespace EcoTrails.Api.Models
{
    public enum TrailDifficultyLevel
    {
        Easy,
        Moderate,
        Difficult
    }

    public class Trail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;

        public int Difficulty { get; set; }

        public TrailDifficultyLevel DifficultyLevel { get; set; } = TrailDifficultyLevel.Moderate;

        public bool WaterSources { get; set; }

        public int? MaxAltitude { get; set; }

        public bool SuitableForKids { get; set; }

        public string RequiredGear { get; set; } = "[]";

        public double DurationInHours { get; set; }

        public int ElevationGain { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserFavoriteTrail> FavoritedByUsers { get; set; } = new List<UserFavoriteTrail>();
    }
}