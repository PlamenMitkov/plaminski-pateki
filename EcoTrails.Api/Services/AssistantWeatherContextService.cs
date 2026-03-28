using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcoTrails.Api.Contracts;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public sealed class AssistantWeatherContextService : IAssistantWeatherContextService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<AssistantWeatherContextService> _logger;

    public AssistantWeatherContextService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<AssistantWeatherContextService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsWeatherPrompt(string prompt)
    {
        var normalized = prompt?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains("врем") ||
               normalized.Contains("температур") ||
               normalized.Contains("дъжд") ||
               normalized.Contains("валеж") ||
               normalized.Contains("прогноз");
    }

    public async Task<string?> BuildWeatherContextAsync(
        string prompt,
        List<AssistantTrailContext> trails,
        CancellationToken cancellationToken)
    {
        if (!_options.WeatherEnabled || !IsWeatherPrompt(prompt))
        {
            return null;
        }

        try
        {
            (double Latitude, double Longitude, string Label)? target = null;

            var trailWithCoordinates = trails.FirstOrDefault(item => item.Latitude.HasValue && item.Longitude.HasValue);
            if (trailWithCoordinates is not null)
            {
                target = (trailWithCoordinates.Latitude!.Value, trailWithCoordinates.Longitude!.Value, trailWithCoordinates.Location);
            }
            else
            {
                var requestedLocation = ExtractLocationFromPrompt(prompt);
                if (!string.IsNullOrWhiteSpace(requestedLocation))
                {
                    target = await GeocodeLocationAsync(requestedLocation, cancellationToken);
                }
            }

            if (target is null)
            {
                return "Няма координати за заявеното място, затова не може да се извлече актуално време.";
            }

            var weather = await GetCurrentWeatherAsync(target.Value.Latitude, target.Value.Longitude, cancellationToken);
            if (weather is null)
            {
                return $"Не успях да заредя актуално време за {target.Value.Label}.";
            }

            var advice = BuildWeatherPreparationAdvice(weather.TemperatureC, weather.PrecipitationMm, weather.WindSpeedKmh);
            return
                $"{target.Value.Label} ({target.Value.Latitude:F4}, {target.Value.Longitude:F4}) | " +
                $"температура {weather.TemperatureC:F1}°C (усеща се {weather.ApparentTemperatureC:F1}°C), " +
                $"вятър {weather.WindSpeedKmh:F1} km/h, валеж {weather.PrecipitationMm:F1} mm, " +
                $"условия: {weather.ConditionDescription}. Подготовка: {advice}.";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to fetch weather context for assistant prompt.");
            return "Актуални метео данни не са налични в момента.";
        }
    }

    private static string ExtractLocationFromPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        var aroundMatch = Regex.Match(prompt, @"(?:около|край|в|до)\s+([\p{L}\-\s]{2,40})", RegexOptions.IgnoreCase);
        if (!aroundMatch.Success)
        {
            return string.Empty;
        }

        var location = aroundMatch.Groups[1].Value.Trim();
        return Regex.Replace(location, @"[^\p{L}\-\s]", string.Empty).Trim();
    }

    private async Task<(double Latitude, double Longitude, string Label)?> GeocodeLocationAsync(
        string location,
        CancellationToken cancellationToken)
    {
        var endpoint =
            $"{_options.WeatherGeocodingBaseUrl.TrimEnd('/')}/search?name={Uri.EscapeDataString(location)}&count=1&language=bg&format=json";

        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(payload);

        if (!document.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            return null;
        }

        var first = results[0];
        if (!first.TryGetProperty("latitude", out var latProp) || !first.TryGetProperty("longitude", out var lonProp))
        {
            return null;
        }

        var latitude = latProp.GetDouble();
        var longitude = lonProp.GetDouble();
        var name = first.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? location : location;

        return (latitude, longitude, name);
    }

    private async Task<WeatherSnapshot?> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var lat = latitude.ToString("F6", CultureInfo.InvariantCulture);
        var lon = longitude.ToString("F6", CultureInfo.InvariantCulture);
        var endpoint =
            $"{_options.WeatherApiBaseUrl.TrimEnd('/')}/forecast?latitude={lat}&longitude={lon}" +
            "&current=temperature_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m&timezone=auto";

        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(payload);

        if (!document.RootElement.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var temperature = current.TryGetProperty("temperature_2m", out var tempProp) && tempProp.ValueKind == JsonValueKind.Number
            ? tempProp.GetDouble()
            : 0;
        var apparent = current.TryGetProperty("apparent_temperature", out var appProp) && appProp.ValueKind == JsonValueKind.Number
            ? appProp.GetDouble()
            : temperature;
        var precipitation = current.TryGetProperty("precipitation", out var precipitationProp) && precipitationProp.ValueKind == JsonValueKind.Number
            ? precipitationProp.GetDouble()
            : 0;
        var wind = current.TryGetProperty("wind_speed_10m", out var windProp) && windProp.ValueKind == JsonValueKind.Number
            ? windProp.GetDouble()
            : 0;
        var weatherCode = current.TryGetProperty("weather_code", out var weatherCodeProp) && weatherCodeProp.ValueKind == JsonValueKind.Number
            ? weatherCodeProp.GetInt32()
            : -1;

        return new WeatherSnapshot
        {
            TemperatureC = temperature,
            ApparentTemperatureC = apparent,
            PrecipitationMm = precipitation,
            WindSpeedKmh = wind,
            ConditionDescription = DescribeWeatherCode(weatherCode)
        };
    }

    private static string DescribeWeatherCode(int code)
    {
        return code switch
        {
            0 => "ясно",
            1 or 2 => "частична облачност",
            3 => "облачно",
            45 or 48 => "мъгла",
            51 or 53 or 55 => "слаб дъжд",
            61 or 63 or 65 => "дъжд",
            66 or 67 => "поледица",
            71 or 73 or 75 => "сняг",
            80 or 81 or 82 => "превалявания",
            95 or 96 or 99 => "буря",
            _ => "променливо"
        };
    }

    private static string BuildWeatherPreparationAdvice(double temperatureC, double precipitationMm, double windSpeedKmh)
    {
        var advice = new List<string>();

        if (temperatureC <= 5)
        {
            advice.Add("слоево облекло и топъл слой");
        }
        else if (temperatureC >= 28)
        {
            advice.Add("шапка, слънцезащита и повече вода");
        }

        if (precipitationMm >= 0.2)
        {
            advice.Add("дъждобран и непромокаеми обувки");
        }

        if (windSpeedKmh >= 25)
        {
            advice.Add("ветроустойчиво яке");
        }

        if (advice.Count == 0)
        {
            advice.Add("стандартна туристическа екипировка и 1-1.5 л вода");
        }

        return string.Join(", ", advice);
    }

    private sealed class WeatherSnapshot
    {
        public double TemperatureC { get; set; }
        public double ApparentTemperatureC { get; set; }
        public double PrecipitationMm { get; set; }
        public double WindSpeedKmh { get; set; }
        public string ConditionDescription { get; set; } = "променливо";
    }
}
