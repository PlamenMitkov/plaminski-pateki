using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public sealed class AssistantResponseCompositionService : IAssistantResponseCompositionService
{
    private readonly IAssistantWeatherContextService _weatherContextService;
    private readonly IAssistantDynamicPromptService _dynamicPromptService;

    public AssistantResponseCompositionService(
        IAssistantWeatherContextService weatherContextService,
        IAssistantDynamicPromptService dynamicPromptService)
    {
        _weatherContextService = weatherContextService;
        _dynamicPromptService = dynamicPromptService;
    }

    public List<AssistantKnowledgeChip> BuildKnowledgeChips(
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives,
        bool hasReliabilityWarning,
        bool isPotentialInjection)
    {
        var chips = new List<AssistantKnowledgeChip>();
        if (trails.Count == 0)
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Няма намерени пътеки", Type = "info" });
            return chips;
        }

        if (hasReliabilityWarning)
        {
            chips.Add(new AssistantKnowledgeChip
            {
                Label = "Част от данните са с ограничена проверимост",
                Type = "warning"
            });
        }

        if (isPotentialInjection)
        {
            chips.Add(new AssistantKnowledgeChip
            {
                Label = "Засечени са подозрителни инструкции в заявката",
                Type = "warning"
            });
        }

        if (trails.Any(item => item.DifficultyLevel == "difficult"))
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Има трудни маршрути в контекста", Type = "warning" });
        }

        if (trails.Any(item => !item.WaterSources))
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Има маршрути без водоизточници", Type = "warning" });
        }

        if (trails.Any(item => item.SuitableForKids))
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Налични са маршрути, подходящи за деца", Type = "positive" });
        }

        if (trails.Any(item => item.HasCoordinates))
        {
            chips.Add(new AssistantKnowledgeChip { Label = "Налични са маршрути с координати", Type = "info" });
        }

        var commonGear = trails
            .SelectMany(item => item.RequiredGear)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .Take(2)
            .ToList();

        chips.AddRange(commonGear.Select(item => new AssistantKnowledgeChip
        {
            Label = $"Препоръчителна екипировка: {item}",
            Type = "gear"
        }));

        if (alternatives.Count > 0)
        {
            chips.Add(new AssistantKnowledgeChip
            {
                Label = BuildAddedAlternativesLabel(alternatives.Count),
                Type = "positive"
            });

            var primaryRegion = trails[0].Region?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(primaryRegion))
            {
                var neighboringRegionCount = alternatives
                    .Where(item => !string.IsNullOrWhiteSpace(item.Region))
                    .Count(item => !string.Equals(item.Region.Trim(), primaryRegion, StringComparison.OrdinalIgnoreCase));

                if (neighboringRegionCount > 0)
                {
                    chips.Add(new AssistantKnowledgeChip
                    {
                        Label = BuildNearbyAlternativesLabel(neighboringRegionCount),
                        Type = "info"
                    });
                }
            }

            foreach (var alternative in alternatives.Take(2))
            {
                chips.Add(new AssistantKnowledgeChip
                {
                    Label = $"По-лека алтернатива: {alternative.Name}",
                    Type = "info"
                });
            }
        }

        return chips;
    }

    public List<AssistantQuickAction> BuildQuickActions(
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives,
        AssistantChatRequest request,
        string prompt)
    {
        var requiredActions = new List<AssistantQuickAction>();
        var optionalActions = new List<AssistantQuickAction>();

        var primaryTrail = trails.FirstOrDefault();
        var primaryLocation = !string.IsNullOrWhiteSpace(primaryTrail?.Location)
            ? primaryTrail.Location.Trim()
            : "България";
        var regionOrLocation = !string.IsNullOrWhiteSpace(primaryTrail?.Region)
            ? primaryTrail.Region.Trim()
            : primaryLocation;

        var mapTrails = trails
            .Where(item => item.HasCoordinates)
            .Take(3)
            .ToList();

        foreach (var mapTrail in mapTrails)
        {
            requiredActions.Add(new AssistantQuickAction
            {
                Id = "show-map",
                Label = $"Карта: {mapTrail.Name}",
                Value = mapTrail.Id.ToString()
            });
        }

        if (primaryTrail is not null)
        {
            var weatherPrompt = _dynamicPromptService.GenerateDynamicLocationPrompt(primaryLocation, regionOrLocation);
            var weatherParts = weatherPrompt.Split("||");
            requiredActions.Add(new AssistantQuickAction
            {
                Id = "weather-now",
                Label = weatherParts[0],
                Value = weatherParts.Length > 1 ? weatherParts[1] : primaryLocation
            });

            var personalizedPrompt = _dynamicPromptService.GeneratePersonalizedIntroPrompt(regionOrLocation);
            var personalizedParts = personalizedPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = personalizedParts[0],
                Value = personalizedParts.Length > 1 ? personalizedParts[1] : personalizedParts[0]
            });

            var detailPrompt = _dynamicPromptService.GenerateDynamicDetailPrompt();
            var detailParts = detailPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = detailParts[0],
                Value = detailParts.Length > 1 ? detailParts[1] : detailParts[0]
            });

            var comparisonPrompt = _dynamicPromptService.GenerateDynamicComparisonPrompt(regionOrLocation);
            var comparisonParts = comparisonPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = comparisonParts[0],
                Value = comparisonParts.Length > 1 ? comparisonParts[1] : comparisonParts[0]
            });

            var gearPrompt = _dynamicPromptService.GenerateDynamicGearPrompt(primaryTrail.Name);
            var gearParts = gearPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = gearParts[0],
                Value = gearParts.Length > 1 ? gearParts[1] : gearParts[0]
            });
        }

        if (trails.Any(item => !item.WaterSources))
        {
            var waterPrompt = _dynamicPromptService.GenerateDynamicWaterPrompt();
            var waterParts = waterPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = waterParts[0],
                Value = waterParts.Length > 1 ? waterParts[1] : waterParts[0]
            });
        }

        if (trails.Any(item => string.Equals(item.DifficultyLevel, "difficult", StringComparison.OrdinalIgnoreCase)))
        {
            var easyPrompt = _dynamicPromptService.GenerateDynamicEasyVariantPrompt();
            var easyParts = easyPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = easyParts[0],
                Value = easyParts.Length > 1 ? easyParts[1] : easyParts[0]
            });
        }

        if (trails.Any(item => item.SuitableForKids))
        {
            var familyPrompt = _dynamicPromptService.GenerateDynamicFamilyPrompt(regionOrLocation);
            var familyParts = familyPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = familyParts[0],
                Value = familyParts.Length > 1 ? familyParts[1] : familyParts[0]
            });
        }

        if (request.FavoriteCount > 0)
        {
            var favoritesPrompt = _dynamicPromptService.GenerateDynamicFavoritesPrompt(request.FavoriteCount);
            var favoritesParts = favoritesPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = favoritesParts[0],
                Value = favoritesParts.Length > 1 ? favoritesParts[1] : favoritesParts[0]
            });
        }

        if (!string.IsNullOrWhiteSpace(request.FilterSummary))
        {
            var filterPrompt = _dynamicPromptService.GenerateDynamicFilterPrompt();
            var filterParts = filterPrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = filterParts[0],
                Value = filterParts.Length > 1 ? filterParts[1] : filterParts[0]
            });
        }

        if (!string.IsNullOrWhiteSpace(prompt) && _weatherContextService.IsWeatherPrompt(prompt))
        {
            var offlinePrompt = _dynamicPromptService.GenerateDynamicOfflinePrompt();
            var offlineParts = offlinePrompt.Split("||");
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = offlineParts[0],
                Value = offlineParts.Length > 1 ? offlineParts[1] : offlineParts[0]
            });
        }

        foreach (var alternative in alternatives)
        {
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "open-trail-details",
                Label = $"Покажи ми детайли за {alternative.Name}",
                Value = alternative.Id.ToString()
            });

            if (alternative.HasCoordinates)
            {
                optionalActions.Add(new AssistantQuickAction
                {
                    Id = "show-map",
                    Label = $"Карта: {alternative.Name}",
                    Value = alternative.Id.ToString()
                });
            }

            if (!string.IsNullOrWhiteSpace(alternative.Location))
            {
                optionalActions.Add(new AssistantQuickAction
                {
                    Id = "weather-now",
                    Label = $"Време около {alternative.Location}",
                    Value = alternative.Location
                });
            }
        }

        Shuffle(optionalActions);

        var result = new List<AssistantQuickAction>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in requiredActions)
        {
            var key = $"{action.Id}|{action.Value}|{action.Label}";
            if (seen.Add(key))
            {
                result.Add(action);
            }
        }

        foreach (var action in optionalActions)
        {
            if (result.Count >= 8)
            {
                break;
            }

            var key = $"{action.Id}|{action.Value}|{action.Label}";
            if (seen.Add(key))
            {
                result.Add(action);
            }
        }

        return result;
    }

    private static string BuildAddedAlternativesLabel(int count)
    {
        return count == 1
            ? "Добавена 1 алтернатива"
            : $"Добавени {count} алтернативи";
    }

    private static string BuildNearbyAlternativesLabel(int count)
    {
        return count == 1
            ? "Добавена 1 близка алтернатива"
            : $"Добавени {count} близки алтернативи";
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
