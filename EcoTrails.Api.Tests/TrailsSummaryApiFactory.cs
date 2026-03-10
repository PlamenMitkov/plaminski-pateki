using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using EcoTrails.Api.Services;

namespace EcoTrails.Api.Tests;

public class TrailsSummaryApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:DisableBackgroundServices"] = "true",
                ["Testing:SkipDataInitialization"] = "true",
                ["Testing:DatabaseName"] = $"trails-summary-tests-{Guid.NewGuid():N}",
                ["Jwt:Key"] = "testing-jwt-key-please-change-1234567890",
                ["Jwt:Issuer"] = "EcoTrails.Api",
                ["Jwt:Audience"] = "EcoTrails.Client"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOpenAiAssistantService>();
            services.AddSingleton<IOpenAiAssistantService, FakeOpenAiAssistantService>();

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            });

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.ForwardDefaultSelector = _ => "Test";
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureCreated();

            if (!dbContext.Trails.Any(item => item.Name == "Rila Panorama"))
            {
                dbContext.Trails.Add(new Trail
                {
                    Name = "Rila Panorama",
                    Description = new string('A', 140),
                    Location = "Rila",
                    Region = "Blagoevgrad",
                    Difficulty = 3,
                    DifficultyLevel = TrailDifficultyLevel.Moderate,
                    DurationInHours = 3.5,
                    ElevationGain = 320,
                    Latitude = 42.1,
                    Longitude = 23.4,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (!dbContext.Trails.Any(item => item.Name == "Vitosha Ring"))
            {
                dbContext.Trails.Add(new Trail
                {
                    Name = "Vitosha Ring",
                    Description = "Кратък маршрут",
                    Location = "Sofia",
                    Region = "Sofia",
                    Difficulty = 2,
                    DifficultyLevel = TrailDifficultyLevel.Easy,
                    DurationInHours = 1.5,
                    ElevationGain = 120,
                    Latitude = 42.6,
                    Longitude = 23.2,
                    CreatedAt = DateTime.UtcNow
                });
            }

            dbContext.SaveChanges();
        });
    }
}
