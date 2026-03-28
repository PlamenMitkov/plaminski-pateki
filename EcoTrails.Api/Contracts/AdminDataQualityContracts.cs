namespace EcoTrails.Api.Contracts;

public sealed class TrailDataQualityResponse
{
    public int TotalTrails { get; set; }
    public int MissingCoordinates { get; set; }
    public int MissingLengthHints { get; set; }
    public int MissingElevationGain { get; set; }
    public int MissingDescription { get; set; }
    public int StaleSourcePreviews { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}
