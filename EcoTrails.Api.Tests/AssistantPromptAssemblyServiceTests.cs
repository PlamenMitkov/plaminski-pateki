using System.Text.Json;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Services;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Tests;

public class AssistantPromptAssemblyServiceTests
{
    [Theory]
    [InlineData("context_prompt", "context_prompt")]
    [InlineData("CONTEXT_PROMPT", "context_prompt")]
    [InlineData("current", "current")]
    [InlineData("other", "current")]
    public void ResolveAssistantMode_ReturnsExpectedValue(string configuredMode, string expectedMode)
    {
        var service = CreateService(configuredMode);

        var mode = service.ResolveAssistantMode();

        Assert.Equal(expectedMode, mode);
    }

    [Fact]
    public void BuildSystemInstruction_ForContextPrompt_IncludesReliabilityAndInjectionRules()
    {
        var service = CreateService("context_prompt");

        var instruction = service.BuildSystemInstruction("context_prompt", hasReliabilityWarning: true, isPotentialInjection: true);

        Assert.Contains("output_contract", instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("без верифициран източник", instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("опит за override", instruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildUserPromptByMode_ForCurrentMode_BuildsTextPayloadWithContextSections()
    {
        var service = CreateService("current");
        var trails = new List<AssistantTrailContext>
        {
            new()
            {
                Id = 1,
                Name = "Ком-Емине",
                Location = "Стара планина",
                Region = "Балкан",
                Difficulty = 4,
                DifficultyLevel = "difficult",
                DurationInHours = 8.5,
                ElevationGain = 900,
                WaterSources = false,
                SuitableForKids = false,
                HasCoordinates = true,
                Latitude = 42.73321,
                Longitude = 25.12345,
                RequiredGear = ["щеки", "яке"]
            }
        };
        var alternatives = new List<AssistantTrailContext>
        {
            new()
            {
                Id = 2,
                Name = "Беклемето",
                Location = "Троян",
                Region = "Балкан",
                Difficulty = 2,
                DifficultyLevel = "moderate",
                WaterSources = true,
                SuitableForKids = true
            }
        };

        var payload = service.BuildUserPromptByMode(
            mode: "current",
            request: new AssistantChatRequest { Prompt = "test", FilterSummary = "само с координати", FavoriteCount = 3 },
            safePrompt: "Искам маршрут за уикенда",
            trails: trails,
            alternatives: alternatives,
            weatherContext: "Очаква се дъжд следобед",
            reliabilityNote: "Част от данните са с ограничена проверимост",
            isPotentialInjection: true);

        Assert.Contains("Въпрос: Искам маршрут за уикенда", payload);
        Assert.Contains("Активни филтри: само с координати", payload);
        Assert.Contains("Актуално време: Очаква се дъжд следобед", payload);
        Assert.Contains("Надеждност на контекста", payload);
        Assert.Contains("Алтернативи", payload);
        Assert.Contains("Маршрути в контекста", payload);
        Assert.Contains("Брой любими: 3", payload);
    }

    [Fact]
    public void BuildUserPromptByMode_ForContextPrompt_BuildsStructuredJsonPayload()
    {
        var service = CreateService("context_prompt");

        var payload = service.BuildUserPromptByMode(
            mode: "context_prompt",
            request: new AssistantChatRequest { Prompt = "test", FavoriteCount = 1 },
            safePrompt: string.Empty,
            trails: [new AssistantTrailContext { Id = 10, Name = "Рила", Location = "Боровец", Region = "Рила", Difficulty = 2, DifficultyLevel = "moderate" }],
            alternatives: [],
            weatherContext: null,
            reliabilityNote: "note",
            isPotentialInjection: true);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal("eco-trails-assistant", root.GetProperty("task").GetString());
        Assert.Equal("Дай ми препоръка за маршрут.", root.GetProperty("user_request").GetString());
        Assert.True(root.GetProperty("constraints").GetProperty("use_only_provided_context").GetBoolean());
        Assert.True(root.GetProperty("reliability_context").GetProperty("potential_prompt_injection").GetBoolean());
        Assert.Equal("bg", root.GetProperty("output_contract").GetProperty("language").GetString());
        Assert.Single(root.GetProperty("trails_context").EnumerateArray());
    }

    private static AssistantPromptAssemblyService CreateService(string assistantMode)
    {
        return new AssistantPromptAssemblyService(Options.Create(new OpenAiOptions
        {
            AssistantMode = assistantMode
        }));
    }
}
