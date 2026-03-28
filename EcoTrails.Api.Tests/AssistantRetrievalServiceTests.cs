using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;
using EcoTrails.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Tests;

public class AssistantRetrievalServiceTests
{
    [Fact]
    public async Task FindRelevantTrailsAsync_PrioritizesExactTokenMatchOverSubstringFalsePositive()
    {
        using var dbContext = TestDbContextFactory.CreateContext();
        dbContext.Trails.AddRange(
            new Trail
            {
                Id = 1001,
                Name = "Рилски панорами",
                Description = "Лека пътека за семейства с вода.",
                Location = "Боровец",
                Region = "Рила",
                Difficulty = 1,
                DifficultyLevel = TrailDifficultyLevel.Easy,
                WaterSources = true,
                SuitableForKids = true,
                DurationInHours = 2.0,
                ElevationGain = 120
            },
            new Trail
            {
                Id = 1002,
                Name = "Открила обзор",
                Description = "По-труден маршрут без вода.",
                Location = "Смолян",
                Region = "Родопи",
                Difficulty = 4,
                DifficultyLevel = TrailDifficultyLevel.Difficult,
                WaterSources = false,
                SuitableForKids = false,
                DurationInHours = 5.0,
                ElevationGain = 700
            });
        await dbContext.SaveChangesAsync();

        var options = Options.Create(new OpenAiOptions
        {
            TopK = 5,
            VectorMultiplier = 2,
            RrfK = 60
        });

        var service = new AssistantRetrievalService(
            dbContext,
            options,
            new ThrowingVectorService(),
            NullLogger<AssistantRetrievalService>.Instance);

        var request = new AssistantChatRequest
        {
            Prompt = "Търся лека пътека в Рила за деца с вода",
            MaxContextTrails = 10
        };

        var results = await service.FindRelevantTrailsAsync(request.Prompt, request, CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.Equal(1001, results[0].Id);
    }

    [Fact]
    public async Task GetAlternativeTrailsAsync_WhenSameRegionIsInsufficient_UsesClosestCoordinateFallback()
    {
        using var dbContext = TestDbContextFactory.CreateContext();
        dbContext.Trails.AddRange(
            new Trail
            {
                Id = 2000,
                Name = "Труден първичен маршрут",
                Description = "Дълъг и натоварващ",
                Location = "Витоша",
                Region = "София",
                Difficulty = 5,
                DifficultyLevel = TrailDifficultyLevel.Difficult,
                DurationInHours = 7.0,
                ElevationGain = 1200,
                Latitude = 42.62,
                Longitude = 23.26
            },
            new Trail
            {
                Id = 2001,
                Name = "Лека алтернатива в региона",
                Description = "Кратка пътека",
                Location = "Витоша",
                Region = "София",
                Difficulty = 1,
                DifficultyLevel = TrailDifficultyLevel.Easy,
                DurationInHours = 2.0,
                ElevationGain = 150,
                Latitude = 42.63,
                Longitude = 23.27
            },
            new Trail
            {
                Id = 2002,
                Name = "Близка външна алтернатива",
                Description = "Извън региона, но близо",
                Location = "Искър",
                Region = "Пазарджик",
                Difficulty = 2,
                DifficultyLevel = TrailDifficultyLevel.Moderate,
                DurationInHours = 3.0,
                ElevationGain = 220,
                Latitude = 42.64,
                Longitude = 23.28
            },
            new Trail
            {
                Id = 2003,
                Name = "Далечна външна алтернатива",
                Description = "Извън региона и далеч",
                Location = "Смолян",
                Region = "Родопи",
                Difficulty = 2,
                DifficultyLevel = TrailDifficultyLevel.Moderate,
                DurationInHours = 3.2,
                ElevationGain = 230,
                Latitude = 41.68,
                Longitude = 24.69
            });
        await dbContext.SaveChangesAsync();

        var service = new AssistantRetrievalService(
            dbContext,
            Options.Create(new OpenAiOptions()),
            new ThrowingVectorService(),
            NullLogger<AssistantRetrievalService>.Instance);

        var request = new AssistantChatRequest
        {
            Prompt = "Маршрутът е твърде труден, искам нещо по-леко",
            MaxContextTrails = 10
        };

        var primaryContext = new List<AssistantTrailContext>
        {
            new()
            {
                Id = 2000,
                Name = "Труден първичен маршрут",
                Location = "Витоша",
                Region = "София",
                Difficulty = 5,
                DifficultyLevel = "difficult"
            }
        };

        var alternatives = await service.GetAlternativeTrailsAsync(request.Prompt, primaryContext, request, CancellationToken.None);

        Assert.Equal(3, alternatives.Count);
        Assert.Equal(2001, alternatives[0].Id);
        Assert.Equal(2002, alternatives[1].Id);
        Assert.Equal(2003, alternatives[2].Id);
    }

    private sealed class ThrowingVectorService : IVectorService
    {
        public Task<VectorEmbeddingResult> CreateEmbeddingAsync(string input, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Vector service disabled for deterministic fallback-ranking tests.");

        public Task<VectorEmbeddingsBatchResult> CreateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Vector service disabled for deterministic fallback-ranking tests.");
    }
}
