using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public sealed class AssistantResponseCompositionService : IAssistantResponseCompositionService
{
    private readonly IAssistantWeatherContextService _weatherContextService;

    public AssistantResponseCompositionService(IAssistantWeatherContextService weatherContextService)
    {
        _weatherContextService = weatherContextService;
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
            requiredActions.Add(new AssistantQuickAction
            {
                Id = "weather-now",
                Label = $"Време сега около {primaryLocation}",
                Value = primaryLocation
            });

            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = $"Дай 3 персонализирани маршрута в {regionOrLocation}",
                Value = $"Препоръчай 3 персонализирани маршрута в {regionOrLocation} с кратко сравнение и ясно предложение кой е най-подходящ за мен."
            });

            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = "Искам по-дълъг и детайлен съвет",
                Value = "Дай по-дълъг и подробен отговор с план стъпка по стъпка: маршрут, време, екипировка, рискове и алтернативи."
            });

            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = $"Сравни ми топ 2 маршрута около {regionOrLocation}",
                Value = $"Сравни 2-те най-подходящи маршрута около {regionOrLocation} по трудност, време, денивелация, вода и подходящост за начинаещ."
            });

            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = $"Каква е екипировката за {primaryTrail.Name}?",
                Value = $"Каква е най-подходящата екипировка за {primaryTrail.Name} според условията и терена?"
            });
        }

        if (trails.Any(item => !item.WaterSources))
        {
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = "Искам маршрут с водоизточник",
                Value = "Препоръчай маршрут с наличен водоизточник и ми кажи къде е критично да нося повече вода."
            });
        }

        if (trails.Any(item => string.Equals(item.DifficultyLevel, "difficult", StringComparison.OrdinalIgnoreCase)))
        {
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = "Дай ми по-лек вариант",
                Value = "Препоръчай ми по-лека алтернатива за начинаещ с по-малка денивелация и по-малък риск."
            });
        }

        if (trails.Any(item => item.SuitableForKids))
        {
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = "Семеен маршрут за деца",
                Value = $"Предложи семеен маршрут около {regionOrLocation}, подходящ за деца, с кратки съвети за безопасност."
            });
        }

        if (request.FavoriteCount > 0)
        {
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = "Съобрази с любимите ми",
                Value = $"Съобрази предложенията с любимите ми пътеки ({request.FavoriteCount}) и обясни защо ги предпочиташ."
            });
        }

        if (!string.IsNullOrWhiteSpace(request.FilterSummary))
        {
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = "Обясни ми избора по филтрите",
                Value = "Обясни защо избираш точно тези пътеки според текущите активни филтри и какво би променил при по-строги условия."
            });
        }

        if (!string.IsNullOrWhiteSpace(prompt) && _weatherContextService.IsWeatherPrompt(prompt))
        {
            optionalActions.Add(new AssistantQuickAction
            {
                Id = "ask-prompt",
                Label = "Дай ми офлайн локация",
                Value = "Ако няма интернет или GPS, как да се ориентирам офлайн по маршрута и кои са ключовите ориентири?"
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
