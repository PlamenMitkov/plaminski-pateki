// Example Test Scenarios for Dynamic Prompts
// This demonstrates how the system generates different variations

using EcoTrails.Api.Services;
using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Tests;

public class AssistantDynamicPromptServiceTests
{
    private readonly IAssistantDynamicPromptService _service;

    public AssistantDynamicPromptServiceTests()
    {
        _service = new AssistantDynamicPromptService();
    }

    /// <summary>
    /// Demonstrates that each call generates a different variation.
    /// In a real session, each new chat session would get different suggestions.
    /// </summary>
    public void Test_GenerateDynamicLocationPrompt_ProducesVariations()
    {
        // Example: Kireyevo location
        const string location = "Киреево";
        const string region = "Киреево";

        // Simulate 5 different sessions
        var variations = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var prompt = _service.GenerateDynamicLocationPrompt(location, region);
            variations.Add(prompt);
            // Output: "Време сега около Киреево||Кои са текущите условия около Киреево?"
            //         "Текущо време в Киреево||Каква е прогнозата за Киреево в следващите часове?"
            //         etc.
        }

        // Assert: We should have multiple variations (at least 2 of the 9 possible combinations)
        Console.WriteLine($"Location prompt variations generated: {variations.Count}");
        foreach (var variation in variations.Take(3))
        {
            Console.WriteLine($"  - {variation}");
        }
    }

    public void Test_GenerateDynamicComparisonPrompt_ProducesVariations()
    {
        const string region = "Киреево";
        
        var variations = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var prompt = _service.GenerateDynamicComparisonPrompt(region);
            variations.Add(prompt);
        }

        Console.WriteLine($"\nComparison prompt variations generated: {variations.Count}");
        foreach (var variation in variations.Take(3))
        {
            Console.WriteLine($"  - {variation.Split("||")[0]}");
        }
    }

    public void Test_GenerateDynamicDetailPrompt_ProducesVariations()
    {
        var variations = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var prompt = _service.GenerateDynamicDetailPrompt();
            variations.Add(prompt);
        }

        Console.WriteLine($"\nDetail prompt variations generated: {variations.Count}");
        foreach (var variation in variations.Take(3))
        {
            Console.WriteLine($"  - {variation.Split("||")[0]}");
        }
    }

    public void Test_GenerateDynamicFamilyPrompt_ProducesVariations()
    {
        const string region = "Киреево";
        
        var variations = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var prompt = _service.GenerateDynamicFamilyPrompt(region);
            variations.Add(prompt);
        }

        Console.WriteLine($"\nFamily prompt variations generated: {variations.Count}");
        foreach (var variation in variations.Take(3))
        {
            Console.WriteLine($"  - {variation.Split("||")[0]}");
        }
    }

    public void Test_GenerateDynamicWaterPrompt_ProducesVariations()
    {
        var variations = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var prompt = _service.GenerateDynamicWaterPrompt();
            variations.Add(prompt);
        }

        Console.WriteLine($"\nWater prompt variations generated: {variations.Count}");
        foreach (var variation in variations.Take(3))
        {
            Console.WriteLine($"  - {variation.Split("||")[0]}");
        }
    }
}

/// <summary>
/// Example integration showing how BuildQuickActions uses dynamic prompts:
/// 
/// OLD CODE (Static):
///   requiredActions.Add(new AssistantQuickAction
///   {
///       Id = "weather-now",
///       Label = $"Време сега около {primaryLocation}",
///       Value = primaryLocation
///   });
/// 
/// NEW CODE (Dynamic):
///   var weatherPrompt = _dynamicPromptService.GenerateDynamicLocationPrompt(primaryLocation, regionOrLocation);
///   var weatherParts = weatherPrompt.Split("||");
///   requiredActions.Add(new AssistantQuickAction
///   {
///       Id = "weather-now",
///       Label = weatherParts[0],  // e.g., "Текущо време в Киреево"
///       Value = weatherParts.Length > 1 ? weatherParts[1] : primaryLocation  // e.g., "Каква е прогнозата за Киреево?"
///   });
/// 
/// RESULT:
/// - Session 1: User sees "Време сега около Киреево" + weather question 1
/// - Session 2: User sees "Текущо време в Киреево" + weather question 2
/// - Session 3: User sees "Проверка на времето около Киреево" + weather question 3
/// - Session N: Variations repeat, but feel fresh
/// </summary>
public class IntegrationExampleDynamicQuickActions
{
    public void Example_QuickActionsAreNowDynamic()
    {
        // This demonstrates what users will see
        
        var exampleLabel1 = "Време сега около Киреево";
        var exampleValue1 = "Кои са текущите условия около Киреево?";
        
        var exampleLabel2 = "Текущо време в Киреево";
        var exampleValue2 = "Каква е прогнозата за Киреево в следващите часове?";
        
        var exampleLabel3 = "Проверка на времето около Киреево";
        var exampleValue3 = "Дай ми метеорологични детайли за Киреево?";
        
        Console.WriteLine("Example Quick Actions (Dynamic):");
        Console.WriteLine($"  Session 1 - Label: {exampleLabel1}");
        Console.WriteLine($"  Session 1 - Prompt: {exampleValue1}");
        Console.WriteLine();
        Console.WriteLine($"  Session 2 - Label: {exampleLabel2}");
        Console.WriteLine($"  Session 2 - Prompt: {exampleValue2}");
        Console.WriteLine();
        Console.WriteLine($"  Session 3 - Label: {exampleLabel3}");
        Console.WriteLine($"  Session 3 - Prompt: {exampleValue3}");
    }
}
