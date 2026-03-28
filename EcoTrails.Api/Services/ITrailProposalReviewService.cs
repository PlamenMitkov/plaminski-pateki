using EcoTrails.Api.Contracts;
using EcoTrails.Api.Models;

namespace EcoTrails.Api.Services;

public interface ITrailProposalReviewService
{
    Task<CommunityPostAiReviewResponse> EvaluateAsync(CommunityTrailPost post, CancellationToken cancellationToken);
    CommunityPostAiReviewResponse BuildFallbackReview(CommunityTrailPost post);
}
