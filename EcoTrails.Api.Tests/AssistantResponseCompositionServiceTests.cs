using EcoTrails.Api.Contracts;
using EcoTrails.Api.Services;

namespace EcoTrails.Api.Tests;

public class AssistantResponseCompositionServiceTests
{
    [Fact]
    public void BuildKnowledgeChips_WhenNoTrails_ReturnsSingleInfoChip()
    {
        var service = new AssistantResponseCompositionService(new StubWeatherContextService(false));

        var chips = service.BuildKnowledgeChips([], [], hasReliabilityWarning: false, isPotentialInjection: false);

        Assert.Single(chips);
        Assert.Equal("Няма намерени пътеки", chips[0].Label);
        Assert.Equal("info", chips[0].Type);
    }

    [Fact]
    public void BuildKnowledgeChips_WithSignalsAndAlternatives_IncludesExpectedChips()
    {
        var service = new AssistantResponseCompositionService(new StubWeatherContextService(false));
        var trails = new List<AssistantTrailContext>
        {
            new()
            {
                Id = 1,
                Name = "Труден маршрут",
                Region = "Рила",
                DifficultyLevel = "difficult",
                WaterSources = false,
                SuitableForKids = false,
                HasCoordinates = true,
                RequiredGear = ["щеки", "щеки", "яке"]
            },
            new()
            {
                Id = 2,
                Name = "Семеен маршрут",
                Region = "Рила",
                DifficultyLevel = "easy",
                WaterSources = true,
                SuitableForKids = true,
                HasCoordinates = false,
                RequiredGear = ["яке"]
            }
        };
        var alternatives = new List<AssistantTrailContext>
        {
            new() { Id = 10, Name = "Алтернатива 1", Region = "Пирин" },
            new() { Id = 11, Name = "Алтернатива 2", Region = "Родопи" }
        };

        var chips = service.BuildKnowledgeChips(trails, alternatives, hasReliabilityWarning: true, isPotentialInjection: true);

        Assert.Contains(chips, item => item.Label.Contains("ограничена проверимост", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("подозрителни инструкции", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("трудни маршрути", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("без водоизточници", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("подходящи за деца", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("с координати", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("Препоръчителна екипировка: щеки", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("Препоръчителна екипировка: яке", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("Добавени 2 алтернативи", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chips, item => item.Label.Contains("Добавени 2 близки алтернативи", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildQuickActions_WithPrimaryTrail_AddsRequiredActionsAndLimitsResult()
    {
        var service = new AssistantResponseCompositionService(new StubWeatherContextService(false));
        var trails = new List<AssistantTrailContext>
        {
            new()
            {
                Id = 7,
                Name = "Витоша",
                Location = "София",
                Region = "София",
                HasCoordinates = true,
                DifficultyLevel = "difficult",
                WaterSources = false,
                SuitableForKids = true
            }
        };

        var actions = service.BuildQuickActions(
            trails,
            alternatives: [new AssistantTrailContext { Id = 9, Name = "Плана", Location = "Железница", Region = "София" }],
            request: new AssistantChatRequest { Prompt = "test", FavoriteCount = 5, FilterSummary = "с координати" },
            prompt: "Маршрут за уикенда");

        Assert.True(actions.Count <= 8);
        Assert.Contains(actions, item => item.Id == "show-map" && item.Value == "7");
        Assert.Contains(actions, item => item.Id == "weather-now" && item.Value == "София");

        var uniqueCount = actions
            .Select(item => $"{item.Id}|{item.Value}|{item.Label}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(uniqueCount, actions.Count);
    }

    [Fact]
    public void BuildQuickActions_WhenWeatherPromptDetected_OffersOfflineGuidanceAction()
    {
        var service = new AssistantResponseCompositionService(new StubWeatherContextService(true));

        var actions = service.BuildQuickActions(
            trails: [],
            alternatives: [],
            request: new AssistantChatRequest { Prompt = "Какво е времето?" },
            prompt: "Какво е времето в района?");

        Assert.Single(actions);
        Assert.Equal("ask-prompt", actions[0].Id);
        Assert.Contains("офлайн", actions[0].Label, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubWeatherContextService : IAssistantWeatherContextService
    {
        private readonly bool _isWeatherPrompt;

        public StubWeatherContextService(bool isWeatherPrompt)
        {
            _isWeatherPrompt = isWeatherPrompt;
        }

        public bool IsWeatherPrompt(string prompt) => _isWeatherPrompt;

        public Task<string?> BuildWeatherContextAsync(string prompt, List<AssistantTrailContext> trails, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }
}
