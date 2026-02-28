using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace EcoTrails.Api.Services;

public class OpenRouteServiceOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openrouteservice.org";
}

public class OpenRouteService
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouteServiceOptions _options;
    private readonly ILogger<OpenRouteService> _logger;

    public OpenRouteService(
        HttpClient httpClient,
        IOptions<OpenRouteServiceOptions> options,
        ILogger<OpenRouteService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<(double Latitude, double Longitude)>?> GetHikingRouteAsync(
        (double Latitude, double Longitude) start,
        (double Latitude, double Longitude) end,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return null;
        }

        try
        {
            var startParam = string.Create(
                CultureInfo.InvariantCulture,
                $"{start.Longitude},{start.Latitude}");
            var endParam = string.Create(
                CultureInfo.InvariantCulture,
                $"{end.Longitude},{end.Latitude}");

            var endpoint =
                $"{_options.BaseUrl.TrimEnd('/')}/v2/directions/foot-hiking" +
                $"?api_key={Uri.EscapeDataString(_options.ApiKey)}" +
                $"&start={Uri.EscapeDataString(startParam)}" +
                $"&end={Uri.EscapeDataString(endParam)}" +
                "&format=geojson";

            using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenRouteService returned status code {StatusCode}", response.StatusCode);
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("features", out var features) ||
                features.ValueKind != JsonValueKind.Array ||
                features.GetArrayLength() == 0)
            {
                return null;
            }

            var firstFeature = features[0];
            if (!firstFeature.TryGetProperty("geometry", out var geometry) ||
                !geometry.TryGetProperty("coordinates", out var coordinates) ||
                coordinates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<(double Latitude, double Longitude)>();
            foreach (var pair in coordinates.EnumerateArray())
            {
                if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2)
                {
                    continue;
                }

                var longitude = pair[0].GetDouble();
                var latitude = pair[1].GetDouble();
                result.Add((latitude, longitude));
            }

            return result.Count >= 2 ? result : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "OpenRouteService request failed");
            return null;
        }
    }
}
