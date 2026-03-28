namespace EcoTrails.Api.Models;

public class TrailEnrichmentSnapshot
{
    public int Id { get; set; }
    public int TrailId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime? SourcePreviewFetchedAtUtc { get; set; }

    public Trail? Trail { get; set; }
}
