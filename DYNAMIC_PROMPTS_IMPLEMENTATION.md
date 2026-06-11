# Dynamic Assistant Hints Implementation - Summary

**Date:** May 17, 2026  
**Language:** Bulgarian UI  
**Issue Resolved:** Static assistant hints that were the same on every session

## Problem Statement
The planning assistant (Планински асистент) was displaying the same static hints/suggestions on every new session:
- "Време сега около Киреево"
- "Подготовка за преход около Киреево"
- "Дай 3 персонализирани маршрута в Киреево"

Users wanted these hints to be **dynamic** - different each time a new session is created.

## Solution Implemented

### 1. New Service: `AssistantDynamicPromptService`
**Location:** `EcoTrails.Api/Services/AssistantDynamicPromptService.cs`

A new service that generates varied assistant prompts dynamically using random selection:

**Variation Arrays (10+ types):**
- **LocationPrompts**: "Време сега около {location}", "Текущо време в {location}", "Проверка на времето около {location}"
- **ComparisonPrompts**: Multiple ways to compare trails
- **DetailPrompts**: Different ways to ask for detailed advice
- **GearPrompts**: Various equipment-related questions
- **WaterPrompts**: Different water source queries
- **EasyVariantPrompts**: Multiple easy alternative requests
- **FamilyPrompts**: Varied family trail suggestions
- **FavoritesPrompts**: Different favorite-related queries
- **FilterPrompts**: Varied filter explanation requests
- **OfflinePrompts**: Different offline navigation questions
- **PersonalizedIntroPrompts**: Multiple personalized trail recommendation variations

**Key Method Signatures:**
```csharp
public string GenerateDynamicLocationPrompt(string location, string region);
public string GenerateDynamicComparisonPrompt(string region);
public string GenerateDynamicDetailPrompt();
public string GenerateDynamicGearPrompt(string trailName);
public string GenerateDynamicWaterPrompt();
public string GenerateDynamicEasyVariantPrompt();
public string GenerateDynamicFamilyPrompt(string region);
public string GenerateDynamicFavoritesPrompt(int favoriteCount);
public string GenerateDynamicFilterPrompt();
public string GenerateDynamicOfflinePrompt();
public string GeneratePersonalizedIntroPrompt(string region);
```

**How It Works:**
- Each method returns a string with format: `{label}||{value}`
- The label is what users see as the quick action button text
- The value is the prompt sent to the AI
- Random selection ensures different combinations on each call

### 2. Updated Service: `AssistantResponseCompositionService`
**Location:** `EcoTrails.Api/Services/AssistantResponseCompositionService.cs`

**Changes:**
- Injected `IAssistantDynamicPromptService` dependency
- Modified `BuildQuickActions()` method to use dynamic prompts
- Replaced all hardcoded quick action labels/values with calls to the dynamic service

**Example Transformation:**
```csharp
// BEFORE (Static):
requiredActions.Add(new AssistantQuickAction
{
    Id = "weather-now",
    Label = $"Време сега около {primaryLocation}",
    Value = primaryLocation
});

// AFTER (Dynamic):
var weatherPrompt = _dynamicPromptService.GenerateDynamicLocationPrompt(primaryLocation, regionOrLocation);
var weatherParts = weatherPrompt.Split("||");
requiredActions.Add(new AssistantQuickAction
{
    Id = "weather-now",
    Label = weatherParts[0],
    Value = weatherParts.Length > 1 ? weatherParts[1] : primaryLocation
});
```

### 3. Dependency Injection Setup
**Location:** `EcoTrails.Api/Program.cs` (Line 82)

Added registration:
```csharp
builder.Services.AddScoped<IAssistantDynamicPromptService, AssistantDynamicPromptService>();
```

## User Experience Impact

### Before (Static Hints)
Every session showed identical suggestions:
```
Session 1:
  - Време сега около Киреево
  - Дай 3 персонализирани маршрута в Киреево
  - Сравни ми топ 2 маршрута около Киреево

Session 2 (Same as Session 1):
  - Време сега около Киреево
  - Дай 3 персонализирани маршрута в Киреево
  - Сравни ми топ 2 маршрута около Киреево
```

### After (Dynamic Hints)
Each session shows varied suggestions:
```
Session 1:
  - Време сега около Киреево
  - Дай 3 персонализирани маршрута в Киреево
  - Сравни ми топ 2 маршрута около Киреево

Session 2 (Different variations):
  - Текущо време в Киреево
  - Предложи ми 3 маршрута в Киреево
  - Какви са най-добрите 2 маршрута в Киреево?

Session 3 (Yet another variation):
  - Проверка на времето около Киреево
  - Кои са 3-те най-добри за мен в Киреево?
  - Поредица на 2-та най-интересни маршрута в Киреево
```

## Technical Details

### Randomization Approach
- Uses `Random.Shared` for thread-safe randomization (C# 11 feature)
- Each label has 2-3 variations
- Each value prompt has 2-3 variations
- Independent random selection for label and value
- Creates 4-9 possible combinations per suggestion type

### Data Flow
1. **User creates new chat session** → `BuildQuickActions()` is called
2. **For each suggestion type**, a dynamic variant is generated:
   - Service randomly selects from label array
   - Service randomly selects from value array
   - Returns combined string with `||` separator
3. **String is split** and assigned to `Label` and `Value` properties
4. **Frontend displays** the dynamic label to user
5. **On click**, sends the dynamic value as prompt to AI

### Context Preservation
- All variations are context-aware (location, region, trail names remain accurate)
- Dynamic variations only affect wording, not meaning
- Same functionality, fresh presentation

## Files Modified/Created

### Created:
1. `EcoTrails.Api/Services/AssistantDynamicPromptService.cs` - New dynamic prompt service
2. `EcoTrails.Api/Tests/AssistantDynamicPromptServiceTests.cs` - Test examples
3. `DYNAMIC_PROMPTS_EXAMPLES.md` - Variation examples documentation

### Modified:
1. `EcoTrails.Api/Services/AssistantResponseCompositionService.cs` - Added DI + dynamic calls
2. `EcoTrails.Api/Program.cs` - Added DI registration

### Documentation:
1. `DYNAMIC_PROMPTS_EXAMPLES.md` - Shows all possible variations

## Verification

✅ **Compilation:** No errors found  
✅ **DI Registration:** Properly configured in Program.cs  
✅ **Service Injection:** Correctly injected in AssistantResponseCompositionService  
✅ **Logic:** Split pattern correctly extracts Label and Value  
✅ **Randomization:** Uses thread-safe Random.Shared  
✅ **Backward Compatibility:** Fallback values ensure no UI breakage  

## Future Enhancements

1. **Seasonal Variations:** Generate different prompts based on season/weather
2. **User Preference Learning:** Track which variations user clicks most
3. **A/B Testing:** Log which variation was shown for analytics
4. **Translation Support:** Add variations in other languages
5. **More Variations:** Expand arrays with 5-10 options each for greater diversity
6. **Contextual Variants:** Adjust prompts based on user difficulty level, preferred regions, etc.

## Testing

Run the included test class to see how variations are generated:
```bash
dotnet test EcoTrails.Api.Tests/AssistantDynamicPromptServiceTests.cs
```

This will demonstrate how multiple different variations are produced from the same context.
