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

            modelBuilder.Entity<Trail>().HasData(
                new Trail
                {
                    Id = 1,
                    Name = "Екопътека \"Ерантис\" – Киреево",
                    Description = "От Киреево тръгва екопътека, която носи името \"Ерантис Булгарикум\" и извежда до защитена местност \"Връшка чука\".",
                    Location = "Киреево",
                    Difficulty = 3,
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
                    Difficulty = 3,
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
                    Difficulty = 4,
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