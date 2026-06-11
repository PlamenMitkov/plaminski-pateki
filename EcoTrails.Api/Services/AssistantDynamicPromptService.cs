using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public interface IAssistantDynamicPromptService
{
    string GenerateDynamicLocationPrompt(string location, string region);
    string GenerateDynamicComparisonPrompt(string region);
    string GenerateDynamicDetailPrompt();
    string GenerateDynamicGearPrompt(string trailName);
    string GenerateDynamicWaterPrompt();
    string GenerateDynamicEasyVariantPrompt();
    string GenerateDynamicFamilyPrompt(string region);
    string GenerateDynamicFavoritesPrompt(int favoriteCount);
    string GenerateDynamicFilterPrompt();
    string GenerateDynamicOfflinePrompt();
    string GenerateDynamicWeatherLabel(string location);
    string GenerateDynamicComparisonLabel(string region);
}

public sealed class AssistantDynamicPromptService : IAssistantDynamicPromptService
{
    private static readonly Random Rng = new();

    private static readonly string[] LocationPrompts = new[]
    {
        "Време сега около {location}",
        "Текущо време в {location}",
        "Проверка на времето около {location}",
    };

    private static readonly string[] LocationPromptValues = new[]
    {
        "Кои са текущите условия около {location}?",
        "Каква е прогнозата за {location} в следващите часове?",
        "Дай ми метеорологични детайли за {location}?",
    };

    private static readonly string[] ComparisonPrompts = new[]
    {
        "Сравни ми топ 2 маршрута около {region}",
        "Какви са най-добрите 2 маршрута в {region}?",
        "Поредица на 2-та най-интересни маршрута в {region}",
    };

    private static readonly string[] ComparisonPromptValues = new[]
    {
        "Сравни 2-те най-подходящи маршрута около {region} по трудност, време, денивелация, вода и подходящост за начинаещ.",
        "Кои са найобмислено подобрани 2 маршрута в {region}? Сравни ги детайлно.",
        "Дай ми дебелинка от топ 2 маршрута в {region} с ясно съобщение кой е по-добър за мен.",
    };

    private static readonly string[] DetailPrompts = new[]
    {
        "Искам по-дълъг и детайлен съвет",
        "Дай ми подробна информация",
        "Разясни ми всичко в детайл",
        "Нужна ми е по-задълбочена препоръка",
    };

    private static readonly string[] DetailPromptValues = new[]
    {
        "Дай по-дълъг и подробен отговор с план стъпка по стъпка: маршрут, време, екипировка, рискове и алтернативи.",
        "Разложи ми препоръката с детайли: маршрут → подготовка → опасности → резервни планове.",
        "Обясни всяка препоръка подробно включително техническите характеристики, условията и съветите за безопасност.",
    };

    private static readonly string[] GearPrompts = new[]
    {
        "Каква е екипировката за {trail}?",
        "Какво трябва да нося за {trail}?",
        "Екипировка за {trail}",
        "Какво съпружение за {trail}?",
    };

    private static readonly string[] GearPromptValues = new[]
    {
        "Каква е най-подходящата екипировка за {trail} според условията и терена?",
        "Направи ми списък с необходимата екипировка за {trail} по приоритет и причина.",
        "Кои са най-критичните елементи на екипировката за {trail} и какво е факултативно?",
    };

    private static readonly string[] WaterPrompts = new[]
    {
        "Искам маршрут с водоизточник",
        "Намери ми маршрут с вода",
        "Маршрути със сигурна вода",
        "Пътеки с водоточиво",
    };

    private static readonly string[] WaterPromptValues = new[]
    {
        "Препоръчай маршрут с наличен водоизточник и ми кажи къде е критично да нося повече вода.",
        "Кои маршрути имат достъп до вода? Избери чистите и безопасни варианти.",
        "Дай ми списък с маршруты, които пресичат река или имат водоточиво по пътя.",
    };

    private static readonly string[] EasyVariantPrompts = new[]
    {
        "Дай ми по-лек вариант",
        "Нужна ми лекотура",
        "Показаи лесни алтернативи",
        "По-проста пътека",
    };

    private static readonly string[] EasyVariantPromptValues = new[]
    {
        "Препоръчай ми по-лека алтернатива за начинаещ с по-малка денивелация и по-малък риск.",
        "Дай ми 2-3 по-лесни варианти подходящи за начинаещ, съчетани със стъпки за градуално подобрение.",
        "Какви са най-лесните маршрути в региона за някой, който тепърва започва?",
    };

    private static readonly string[] FamilyPrompts = new[]
    {
        "Семеен маршрут за деца",
        "Пътека за цялото семейство",
        "Детски маршрут",
        "Маршрут за малката",
    };

    private static readonly string[] FamilyPromptValues = new[]
    {
        "Предложи семеен маршрут около {region}, подходящ за деца, с кратки съвети за безопасност.",
        "Какви са безопасните и забавни маршруты около {region} за семейство с деца?",
        "Дай ми идея за семеен излет около {region} който да е забавен, безопасен и не твърде дълъг.",
    };

    private static readonly string[] FavoritesPrompts = new[]
    {
        "Съобрази с любимите ми",
        "Вземи в предвид любимото",
        "Свързани с любимите",
    };

    private static readonly string[] FavoritesPromptValues = new[]
    {
        "Съобрази предложенията с любимите ми пътеки ({count}) и обясни защо ги предпочиташ.",
        "Как се свързват текущите препоръки с любимите ми {count} пътеки? Вижди ли паттерн?",
        "Вземи предвид, че имам {count} любими пътеки. Какво казват те за моите предпочитания?",
    };

    private static readonly string[] FilterPrompts = new[]
    {
        "Обясни ми избора по филтрите",
        "Защо точно тези маршрути?",
        "Логика на филтрите",
    };

    private static readonly string[] FilterPromptValues = new[]
    {
        "Обясни защо избираш точно тези пътеки според текущите активни филтри и какво би променил при по-строги условия.",
        "Как точно филтрите повлияха на препоръката? Какво се променя при друга настройка?",
        "Покажи ми логиката: кои филтри елиминираха кои маршрути и защо?",
    };

    private static readonly string[] OfflinePrompts = new[]
    {
        "Дай ми офлайн локация",
        "Офлайн навигация",
        "Без интернет начин",
        "Ориентиране без GPS",
    };

    private static readonly string[] OfflinePromptValues = new[]
    {
        "Ако няма интернет или GPS, как да се ориентирам офлайн по маршрута и кои са ключовите ориентири?",
        "Какви е стратегията за безопасност, ако загубя GPS и мобилна връзка?",
        "Как могу да се ориентирам без технология? Дай ми ориентири и забележки.",
    };

    private static readonly string[] PersonalizedIntroPrompts = new[]
    {
        "Дай 3 персонализирани маршрута в {region}",
        "Предложи ми 3 маршрута в {region}",
        "Кои са 3-те най-добри за мен в {region}?",
        "Препоръчай топ 3 пътеки в {region}",
    };

    private static readonly string[] PersonalizedIntroPromptValues = new[]
    {
        "Препоръчай 3 персонализирани маршрута в {region} с кратко сравнение и ясно предложение кой е най-подходящ за мен.",
        "Кои са топ 3 маршрута в {region} според моя профил? Защо точно тези?",
        "Направи ми подреден списък на 3-те най-интересни маршрута в {region} с причини.",
    };

    public string GenerateDynamicLocationPrompt(string location, string region)
    {
        var labelIdx = Rng.Next(LocationPrompts.Length);
        var valueIdx = Rng.Next(LocationPromptValues.Length);
        
        var label = LocationPrompts[labelIdx].Replace("{location}", location);
        var value = LocationPromptValues[valueIdx].Replace("{location}", location);
        
        return $"{label}||{value}";
    }

    public string GenerateDynamicComparisonPrompt(string region)
    {
        var labelIdx = Rng.Next(ComparisonPrompts.Length);
        var valueIdx = Rng.Next(ComparisonPromptValues.Length);
        
        var label = ComparisonPrompts[labelIdx].Replace("{region}", region);
        var value = ComparisonPromptValues[valueIdx].Replace("{region}", region);
        
        return $"{label}||{value}";
    }

    public string GenerateDynamicDetailPrompt()
    {
        var idx = Rng.Next(DetailPrompts.Length);
        var valueIdx = Rng.Next(DetailPromptValues.Length);
        
        return $"{DetailPrompts[idx]}||{DetailPromptValues[valueIdx]}";
    }

    public string GenerateDynamicGearPrompt(string trailName)
    {
        var labelIdx = Rng.Next(GearPrompts.Length);
        var valueIdx = Rng.Next(GearPromptValues.Length);
        
        var label = GearPrompts[labelIdx].Replace("{trail}", trailName);
        var value = GearPromptValues[valueIdx].Replace("{trail}", trailName);
        
        return $"{label}||{value}";
    }

    public string GenerateDynamicWaterPrompt()
    {
        var labelIdx = Rng.Next(WaterPrompts.Length);
        var valueIdx = Rng.Next(WaterPromptValues.Length);
        
        return $"{WaterPrompts[labelIdx]}||{WaterPromptValues[valueIdx]}";
    }

    public string GenerateDynamicEasyVariantPrompt()
    {
        var labelIdx = Rng.Next(EasyVariantPrompts.Length);
        var valueIdx = Rng.Next(EasyVariantPromptValues.Length);
        
        return $"{EasyVariantPrompts[labelIdx]}||{EasyVariantPromptValues[valueIdx]}";
    }

    public string GenerateDynamicFamilyPrompt(string region)
    {
        var labelIdx = Rng.Next(FamilyPrompts.Length);
        var valueIdx = Rng.Next(FamilyPromptValues.Length);
        
        var label = FamilyPrompts[labelIdx];
        var value = FamilyPromptValues[valueIdx].Replace("{region}", region);
        
        return $"{label}||{value}";
    }

    public string GenerateDynamicFavoritesPrompt(int favoriteCount)
    {
        var labelIdx = Rng.Next(FavoritesPrompts.Length);
        var valueIdx = Rng.Next(FavoritesPromptValues.Length);
        
        var label = FavoritesPrompts[labelIdx];
        var value = FavoritesPromptValues[valueIdx].Replace("{count}", favoriteCount.ToString());
        
        return $"{label}||{value}";
    }

    public string GenerateDynamicFilterPrompt()
    {
        var labelIdx = Rng.Next(FilterPrompts.Length);
        var valueIdx = Rng.Next(FilterPromptValues.Length);
        
        return $"{FilterPrompts[labelIdx]}||{FilterPromptValues[valueIdx]}";
    }

    public string GenerateDynamicOfflinePrompt()
    {
        var labelIdx = Rng.Next(OfflinePrompts.Length);
        var valueIdx = Rng.Next(OfflinePromptValues.Length);
        
        return $"{OfflinePrompts[labelIdx]}||{OfflinePromptValues[valueIdx]}";
    }

    public string GenerateDynamicWeatherLabel(string location)
    {
        return GenerateDynamicLocationPrompt(location, location);
    }

    public string GenerateDynamicComparisonLabel(string region)
    {
        return GenerateDynamicComparisonPrompt(region);
    }

    public string GeneratePersonalizedIntroPrompt(string region)
    {
        var labelIdx = Rng.Next(PersonalizedIntroPrompts.Length);
        var valueIdx = Rng.Next(PersonalizedIntroPromptValues.Length);
        
        var label = PersonalizedIntroPrompts[labelIdx].Replace("{region}", region);
        var value = PersonalizedIntroPromptValues[valueIdx].Replace("{region}", region);
        
        return $"{label}||{value}";
    }
}
