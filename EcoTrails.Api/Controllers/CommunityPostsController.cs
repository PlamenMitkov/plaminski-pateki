using System.Security.Claims;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using EcoTrails.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CommunityPostsController : ControllerBase
{
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
    ];

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ITrailProposalReviewService _reviewService;
    private readonly ILogger<CommunityPostsController> _logger;

    public CommunityPostsController(
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        ITrailProposalReviewService reviewService,
        ILogger<CommunityPostsController> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _reviewService = reviewService;
        _logger = logger;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<CommunityPostResponse>>> GetMyPosts(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var posts = await _dbContext.CommunityTrailPosts
            .AsNoTracking()
            .Include(item => item.Trail)
            .Include(item => item.Images)
            .Where(item => item.AppUserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(40)
            .ToListAsync(cancellationToken);

        var response = posts.Select(post => MapPost(post)).ToList();
        return Ok(response);
    }

    [HttpPost("mine")]
    [RequestSizeLimit(30_000_000)]
    public async Task<ActionResult<CommunityPostResponse>> CreateMyPost(
        [FromForm] string title,
        [FromForm] string content,
        [FromForm] int? trailId,
        [FromForm] string? postType,
        [FromForm] List<IFormFile>? images,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var normalizedTitle = title?.Trim() ?? string.Empty;
        var normalizedContent = content?.Trim() ?? string.Empty;

        if (normalizedTitle.Length < 4 || normalizedTitle.Length > 180)
        {
            return BadRequest("Заглавието трябва да е между 4 и 180 символа.");
        }

        if (normalizedContent.Length < 12 || normalizedContent.Length > 6000)
        {
            return BadRequest("Текстът трябва да е между 12 и 6000 символа.");
        }

        Trail? trail = null;
        if (trailId.HasValue)
        {
            trail = await _dbContext.Trails.FirstOrDefaultAsync(item => item.Id == trailId.Value, cancellationToken);
            if (trail is null)
            {
                return BadRequest("Невалиден TrailId.");
            }
        }

        var files = images ?? [];
        if (files.Count > 4)
        {
            return BadRequest("Може да качиш до 4 снимки към една публикация.");
        }

        foreach (var file in files)
        {
            if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest("Разрешени са само JPG, PNG и WEBP изображения.");
            }

            if (file.Length > 7_000_000)
            {
                return BadRequest("Всяка снимка трябва да е до 7MB.");
            }
        }

        var parsedPostType = ParsePostType(postType);
        var post = new CommunityTrailPost
        {
            AppUserId = userId,
            TrailId = trail?.Id,
            Title = normalizedTitle,
            Content = normalizedContent,
            PostType = parsedPostType,
            ProposalStatus = parsedPostType == CommunityPostType.TrailProposal
                ? ProposalStatus.Pending
                : ProposalStatus.None,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _dbContext.CommunityTrailPosts.Add(post);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (files.Count > 0)
        {
            var imagesDirectory = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "community", userId);
            Directory.CreateDirectory(imagesDirectory);

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
                {
                    extension = ".jpg";
                }

                var generatedFileName = $"{Guid.NewGuid():N}{extension}";
                var storagePath = Path.Combine(imagesDirectory, generatedFileName);

                await using (var stream = System.IO.File.Create(storagePath))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                var publicUrl = $"/uploads/community/{Uri.EscapeDataString(userId)}/{generatedFileName}";
                _dbContext.CommunityTrailPostImages.Add(new CommunityTrailPostImage
                {
                    CommunityTrailPostId = post.Id,
                    ImageUrl = publicUrl,
                    StoragePath = storagePath,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var savedPost = await _dbContext.CommunityTrailPosts
            .AsNoTracking()
            .Include(item => item.Trail)
            .Include(item => item.Images)
            .FirstAsync(item => item.Id == post.Id, cancellationToken);

        return Ok(MapPost(savedPost));
    }

    [HttpPut("mine/{postId:int}")]
    public async Task<ActionResult<CommunityPostResponse>> UpdateMyPost(
        int postId,
        [FromBody] CommunityPostUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var post = await _dbContext.CommunityTrailPosts
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == postId && item.AppUserId == userId, cancellationToken);

        if (post is null)
        {
            return NotFound();
        }

        var normalizedTitle = request.Title?.Trim() ?? string.Empty;
        var normalizedContent = request.Content?.Trim() ?? string.Empty;

        if (normalizedTitle.Length < 4 || normalizedTitle.Length > 180)
        {
            return BadRequest("Заглавието трябва да е между 4 и 180 символа.");
        }

        if (normalizedContent.Length < 12 || normalizedContent.Length > 6000)
        {
            return BadRequest("Текстът трябва да е между 12 и 6000 символа.");
        }

        if (request.TrailId.HasValue)
        {
            var trailExists = await _dbContext.Trails.AnyAsync(item => item.Id == request.TrailId.Value, cancellationToken);
            if (!trailExists)
            {
                return BadRequest("Невалиден TrailId.");
            }
        }

        post.Title = normalizedTitle;
        post.Content = normalizedContent;
        post.TrailId = request.TrailId;
        post.PostType = ParsePostType(request.PostType);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedPost = await _dbContext.CommunityTrailPosts
            .AsNoTracking()
            .Include(item => item.Trail)
            .Include(item => item.Images)
            .FirstAsync(item => item.Id == post.Id, cancellationToken);

        return Ok(MapPost(savedPost));
    }

    [HttpDelete("mine/{postId:int}")]
    public async Task<IActionResult> DeleteMyPost(int postId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var post = await _dbContext.CommunityTrailPosts
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == postId && item.AppUserId == userId, cancellationToken);

        if (post is null)
        {
            return NotFound();
        }

        foreach (var image in post.Images)
        {
            if (string.IsNullOrWhiteSpace(image.StoragePath))
            {
                continue;
            }

            try
            {
                if (System.IO.File.Exists(image.StoragePath))
                {
                    System.IO.File.Delete(image.StoragePath);
                }
            }
            catch
            {
                // The database delete is authoritative; file cleanup is best effort.
            }
        }

        _dbContext.CommunityTrailPosts.Remove(post);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("trail/{trailId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CommunityPostResponse>>> GetTrailPosts(
        int trailId,
        CancellationToken cancellationToken)
    {
        var trailExists = await _dbContext.Trails.AnyAsync(t => t.Id == trailId, cancellationToken);
        if (!trailExists)
        {
            return NotFound();
        }

        var posts = await _dbContext.CommunityTrailPosts
            .AsNoTracking()
            .Include(item => item.Trail)
            .Include(item => item.Images)
            .Where(item => item.TrailId == trailId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(posts.Select(post => MapPost(post)).ToList());
    }

    [HttpGet("admin/pending-trail-proposals")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<CommunityPostResponse>>> GetPendingTrailProposals(CancellationToken cancellationToken)
    {
        var posts = await _dbContext.CommunityTrailPosts
            .AsNoTracking()
            .Include(item => item.Trail)
            .Include(item => item.Images)
            .Where(item => item.PostType == CommunityPostType.TrailProposal
                && item.ProposalStatus != ProposalStatus.Approved
                && item.ProposalStatus != ProposalStatus.Rejected)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var response = new List<CommunityPostResponse>(posts.Count);
        foreach (var post in posts)
        {
            var aiReview = await _reviewService.EvaluateAsync(post, cancellationToken);
            response.Add(MapPost(post, aiReview));
        }

        return Ok(response);
    }

    [HttpPost("admin/{postId:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Trail>> ApproveTrailProposal(
        int postId,
        [FromBody] CommunityPostApproveRequest? request,
        CancellationToken cancellationToken)
    {
        var post = await _dbContext.CommunityTrailPosts
            .Include(item => item.Trail)
            .FirstOrDefaultAsync(item => item.Id == postId, cancellationToken);

        if (post is null)
        {
            return NotFound();
        }

        if (post.PostType != CommunityPostType.TrailProposal)
        {
            return BadRequest("Постът не е маркиран като предложение за нова пътека.");
        }

        if (post.TrailId.HasValue)
        {
            return Conflict("Това предложение вече е обвързано с пътека.");
        }

        var aiReview = await _reviewService.EvaluateAsync(post, cancellationToken);

        var trail = new Trail
        {
            Name = string.IsNullOrWhiteSpace(request?.Name)
                ? (!string.IsNullOrWhiteSpace(aiReview.SuggestedName) ? aiReview.SuggestedName : post.Title)
                : request!.Name.Trim(),
            Description = post.Content.Trim(),
            Location = string.IsNullOrWhiteSpace(request?.Location)
                ? (!string.IsNullOrWhiteSpace(aiReview.SuggestedLocation) ? aiReview.SuggestedLocation : "Неуточнена")
                : request!.Location.Trim(),
            Region = string.IsNullOrWhiteSpace(request?.Region)
                ? (!string.IsNullOrWhiteSpace(aiReview.SuggestedRegion) ? aiReview.SuggestedRegion : "Неуточнен")
                : request!.Region.Trim(),
            DifficultyLevel = ParseDifficultyLevel(request?.DifficultyLevel ?? aiReview.SuggestedDifficultyLevel),
            DurationInHours = request?.DurationInHours is > 0 ? request.DurationInHours.Value : 2.0,
            ElevationGain = request?.ElevationGain is > 0 ? request.ElevationGain.Value : 250,
            Latitude = request?.Latitude,
            Longitude = request?.Longitude,
            WaterSources = request?.WaterSources ?? false,
            SuitableForKids = request?.SuitableForKids ?? false,
            MaxAltitude = request?.MaxAltitude,
            RequiredGear = string.IsNullOrWhiteSpace(request?.RequiredGearJson)
                ? "[]"
                : request!.RequiredGearJson.Trim(),
            Difficulty = ToLegacyDifficulty(ParseDifficultyLevel(request?.DifficultyLevel ?? aiReview.SuggestedDifficultyLevel)),
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Trails.Add(trail);
        await _dbContext.SaveChangesAsync(cancellationToken);

        post.TrailId = trail.Id;
        post.ProposalStatus = ProposalStatus.Approved;
        post.ReviewedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(trail);
    }

    private static CommunityPostResponse MapPost(CommunityTrailPost post, CommunityPostAiReviewResponse? aiReview = null)
    {
        return new CommunityPostResponse
        {
            Id = post.Id,
            TrailId = post.TrailId,
            TrailName = post.Trail?.Name ?? string.Empty,
            Title = post.Title,
            Content = post.Content,
            PostType = post.PostType.ToString(),
            ProposalStatus = post.ProposalStatus.ToString(),
            RejectionReason = post.RejectionReason,
            CreatedAtUtc = post.CreatedAtUtc,
            ImageUrls = post.Images.Select(item => item.ImageUrl).ToList(),
            AiReview = aiReview,
        };
    }

    private static CommunityPostType ParsePostType(string? postType)
    {
        var normalized = postType?.Trim();
        if (string.Equals(normalized, nameof(CommunityPostType.TrailFeedback), StringComparison.OrdinalIgnoreCase))
        {
            return CommunityPostType.TrailFeedback;
        }

        if (string.Equals(normalized, nameof(CommunityPostType.TrailProposal), StringComparison.OrdinalIgnoreCase))
        {
            return CommunityPostType.TrailProposal;
        }

        return CommunityPostType.General;
    }

    private static TrailDifficultyLevel ParseDifficultyLevel(string? value)
    {
        if (Enum.TryParse<TrailDifficultyLevel>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return TrailDifficultyLevel.Moderate;
    }

    private static int ToLegacyDifficulty(TrailDifficultyLevel level)
    {
        return level switch
        {
            TrailDifficultyLevel.Easy => 2,
            TrailDifficultyLevel.Moderate => 3,
            TrailDifficultyLevel.Difficult => 4,
            _ => 3,
        };
    }
}
