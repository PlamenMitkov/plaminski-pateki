using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;

namespace EcoTrails.Api.Services;

public interface IAssistantEnrichmentWorkflowService
{
    Task<AssistantEnrichResponse> ExecuteAsync(
        AssistantEnrichRequest request,
        Func<Trail, CancellationToken, Task<AssistantTrailSemanticData>> extractSemanticDataAsync,
        CancellationToken cancellationToken);
}

public sealed class AssistantTrailSemanticData
{
    public TrailDifficultyLevel DifficultyLevel { get; set; }
    public bool WaterSources { get; set; }
    public int? MaxAltitude { get; set; }
    public bool SuitableForKids { get; set; }
    public List<string> RequiredGear { get; set; } = [];
}
