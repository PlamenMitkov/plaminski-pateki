namespace EcoTrails.Api.Contracts;

public sealed class TrailOfflineEnrichmentResponse
{
    public DateTime GeneratedAt { get; set; }
    public int RequestedTrailCount { get; set; }
    public int EnrichedTrailCount { get; set; }
    public int CachedTrailCount { get; set; }
    public int SourcePreviewCount { get; set; }
    public int SourcePreviewFailures { get; set; }
    public LiveWeatherAlertsSummary WeatherAlerts { get; set; } = new();
    public IReadOnlyList<TrailOfflineEnrichmentItem> Trails { get; set; } = [];
}

public sealed class LiveWeatherAlertsSummary
{
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
    public bool IsOfficialSource { get; set; }
    public IReadOnlyList<string> Alerts { get; set; } = [];
}

public sealed class TrailOfflineEnrichmentItem
{
    public int TrailId { get; set; }
    public string TrailName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime CachedAt { get; set; }
    public string? SourceUrl { get; set; }
    public TrailSourcePreview? SourcePreview { get; set; }
    public TrailTransportInfo Transport { get; set; } = new();
    public TrailAccessibilityInfo Accessibility { get; set; } = new();
    public IReadOnlyList<string> SafetyWarnings { get; set; } = [];
    public IReadOnlyList<string> NearbyAmenities { get; set; } = [];
    public IReadOnlyList<string> EquipmentNeeded { get; set; } = [];
    public IReadOnlyList<string> SuitabilityTags { get; set; } = [];
    public IReadOnlyList<string> BestMonths { get; set; } = [];
    public bool? WinterAccessible { get; set; }
    public bool? WeatherDependent { get; set; }
    public IReadOnlyList<string> WebcamLinks { get; set; } = [];
    public IReadOnlyList<string> AccessNotes { get; set; } = [];
}

public sealed class TrailSourcePreview
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime FetchedAt { get; set; }
}

public sealed class TrailTransportInfo
{
    public string? PublicTransport { get; set; }
    public bool? ParkingAvailable { get; set; }
}

public sealed class TrailAccessibilityInfo
{
    public bool? WheelchairAccessible { get; set; }
    public bool? StrollerFriendly { get; set; }
    public bool? BicycleAllowed { get; set; }
}
