using EcoTrails.Api.Contracts;

namespace EcoTrails.Api.Services;

public interface IAssistantProvenancePolicyService
{
    Task<AssistantProvenanceContextResult> BuildContextAsync(
        List<AssistantTrailContext> trails,
        List<AssistantTrailContext> alternatives,
        CancellationToken cancellationToken);
}

public sealed record AssistantProvenanceContextResult(
    List<AssistantTrailContext> Trails,
    List<AssistantTrailContext> Alternatives,
    bool HasReliabilityWarning,
    string? ReliabilityNote);
