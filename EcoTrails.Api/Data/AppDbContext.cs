using EcoTrails.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Trail> Trails { get; set; }
        public DbSet<UserFavoriteTrail> UserFavoriteTrails { get; set; }
        public DbSet<AssistantChatSession> AssistantChatSessions { get; set; }
        public DbSet<AssistantChatEntry> AssistantChatEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserFavoriteTrail>()
                .HasKey(item => new { item.UserId, item.TrailId });

            modelBuilder.Entity<UserFavoriteTrail>()
                .HasOne(item => item.User)
                .WithMany(user => user.FavoriteTrails)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserFavoriteTrail>()
                .HasOne(item => item.Trail)
                .WithMany(trail => trail.FavoritedByUsers)
                .HasForeignKey(item => item.TrailId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssistantChatSession>()
                .HasIndex(item => item.SessionId)
                .IsUnique();

            modelBuilder.Entity<AssistantChatSession>()
                .Property(item => item.SessionId)
                .HasMaxLength(64);

            modelBuilder.Entity<AssistantChatSession>()
                .Property(item => item.Title)
                .HasMaxLength(180);

            modelBuilder.Entity<AssistantChatSession>()
                .HasIndex(item => item.AppUserId);

            modelBuilder.Entity<AssistantChatEntry>()
                .Property(item => item.Role)
                .HasMaxLength(24);

            modelBuilder.Entity<AssistantChatEntry>()
                .HasOne(item => item.Session)
                .WithMany(session => session.Messages)
                .HasForeignKey(item => item.SessionInternalId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Trail>()
                .Property(item => item.DifficultyLevel)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasSentinel(TrailDifficultyLevel.Moderate)
                .HasDefaultValue(TrailDifficultyLevel.Moderate);

            modelBuilder.Entity<Trail>()
                .Property(item => item.RequiredGear)
                .HasMaxLength(1200)
                .HasDefaultValue("[]");

            modelBuilder.Entity<Trail>()
                .Property(item => item.WaterSources)
                .HasDefaultValue(false);

            modelBuilder.Entity<Trail>()
                .Property(item => item.SuitableForKids)
                .HasDefaultValue(false);

            modelBuilder.Entity<Trail>()
                .Property(item => item.Region)
                .HasMaxLength(120)
                .HasDefaultValue(string.Empty);

            modelBuilder.Entity<Trail>()
                .Property(item => item.EmbeddingVector)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<Trail>()
                .Property(item => item.EmbeddingModel)
                .HasMaxLength(120);

            modelBuilder.Entity<Trail>().HasData(
                new Trail
                {
                    Id = 1,
                    Name = "Екопътека \"Ерантис\" – Киреево",
                    Description = "От Киреево тръгва екопътека, която носи името \"Ерантис Булгарикум\" и извежда до защитена местност \"Връшка чука\".",
                    Location = "Киреево",
                    Region = "Видин",
                    Difficulty = 3,
                    DifficultyLevel = TrailDifficultyLevel.Moderate,
                    WaterSources = false,
                    MaxAltitude = 780,
                    SuitableForKids = true,
                    RequiredGear = "[\"туристически обувки\",\"вода\",\"дъждобран\"]",
                    DurationInHours = 2.5,
                    ElevationGain = 200,
                    Latitude = 43.794448,
                    Longitude = 22.394714,
                    CreatedAt = new DateTime(2026, 2, 4, 16, 36, 11, DateTimeKind.Utc)
                },
                new Trail
                {
                    Id = 2,
                    Name = "Екопътека \"Етър-Соколски манастир\"",
                    Description = "Трасето на пътеката минава през гориста местност и свързва Етъра и Соколския манастир.",
                    Location = "Етър",
                    Region = "Габрово",
                    Difficulty = 3,
                    DifficultyLevel = TrailDifficultyLevel.Moderate,
                    WaterSources = true,
                    MaxAltitude = 560,
                    SuitableForKids = true,
                    RequiredGear = "[\"удобни обувки\",\"вода\",\"лека връхна дреха\"]",
                    DurationInHours = 2.0,
                    ElevationGain = 120,
                    Latitude = 42.79731,
                    Longitude = 25.33827,
                    CreatedAt = new DateTime(2026, 2, 4, 16, 36, 11, DateTimeKind.Utc)
                },
                new Trail
                {
                    Id = 3,
                    Name = "Екопътека \"Збегове\" – Белоградчик",
                    Description = "Средно тежък кръгов маршрут с панорамни гледки към Белоградчишките скали и връх Ведерник.",
                    Location = "Белоградчик",
                    Region = "Видин",
                    Difficulty = 4,
                    DifficultyLevel = TrailDifficultyLevel.Difficult,
                    WaterSources = false,
                    MaxAltitude = 1060,
                    SuitableForKids = false,
                    RequiredGear = "[\"високи туристически обувки\",\"щеки\",\"вода\",\"слойна екипировка\"]",
                    DurationInHours = 4.0,
                    ElevationGain = 450,
                    Latitude = 43.625169,
                    Longitude = 22.686319,
                    CreatedAt = new DateTime(2026, 2, 4, 16, 36, 11, DateTimeKind.Utc)
                }
            );
        }
    }
}