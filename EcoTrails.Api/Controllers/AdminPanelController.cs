using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using EcoTrails.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EcoTrails.Api.Controllers;

[Route("api/adminpanel")]
[ApiController]
public class AdminPanelController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly AdminPanelOptions _adminPanelOptions;
    private readonly JwtOptions _jwtOptions;
    private readonly ITrailProposalReviewService _reviewService;
    private readonly ILogger<AdminPanelController> _logger;

    public AdminPanelController(
        AppDbContext dbContext,
        IOptions<AdminPanelOptions> adminPanelOptions,
        IOptions<JwtOptions> jwtOptions,
        ITrailProposalReviewService reviewService,
        ILogger<AdminPanelController> logger)
    {
        _dbContext = dbContext;
        _adminPanelOptions = adminPanelOptions.Value;
        _jwtOptions = jwtOptions.Value;
        _reviewService = reviewService;
        _logger = logger;
    }

    // ─── Login ───────────────────────────────────────────────────────────────

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] AdminPanelLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(_adminPanelOptions.Username) ||
            string.IsNullOrWhiteSpace(_adminPanelOptions.Password))
        {
            _logger.LogWarning("Admin panel login attempted but credentials are not configured.");
            return StatusCode(503, "Админ панелът не е конфигуриран.");
        }

        if (!VerifyCredentials(request.Username, request.Password))
        {
            _logger.LogWarning("Admin panel login failed for username {Username}.", request.Username);
            return Unauthorized("Невалидни потребителско име или парола.");
        }

        var token = CreatePanelToken(request.Username);
        _logger.LogInformation("Admin panel login succeeded for {Username}.", request.Username);
        return Ok(new { token });
    }

    // ─── Proposals ───────────────────────────────────────────────────────────

    [HttpGet("proposals")]
    [Authorize(Roles = "AdminPanel")]
    public async Task<ActionResult<IReadOnlyList<CommunityPostResponse>>> GetProposals(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.CommunityTrailPosts
            .AsNoTracking()
            .Include(item => item.Trail)
            .Include(item => item.Images)
            .Where(item => item.PostType == CommunityPostType.TrailProposal);

        if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item =>
                item.ProposalStatus == ProposalStatus.Pending ||
                item.ProposalStatus == ProposalStatus.None);
        }
        else if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => item.ProposalStatus == ProposalStatus.Approved);
        }
        else if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => item.ProposalStatus == ProposalStatus.Rejected);
        }

        var posts = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var response = new List<CommunityPostResponse>(posts.Count);
        foreach (var post in posts)
        {
            var aiReview = await _reviewService.EvaluateAsync(post, cancellationToken);
            response.Add(MapPost(post, aiReview));
        }

        return Ok(response);
    }

    [HttpPost("{postId:int}/approve")]
    [Authorize(Roles = "AdminPanel")]
    public async Task<ActionResult<object>> ApproveProposal(
        int postId,
        [FromBody] CommunityPostApproveRequest? request,
        CancellationToken cancellationToken)
    {
        var post = await _dbContext.CommunityTrailPosts
            .FirstOrDefaultAsync(item => item.Id == postId, cancellationToken);

        if (post is null) return NotFound();

        if (post.PostType != CommunityPostType.TrailProposal)
            return BadRequest("Постът не е предложение за пътека.");

        if (post.ProposalStatus == ProposalStatus.Approved)
            return Conflict("Предложението вече е одобрено.");

        if (post.ProposalStatus == ProposalStatus.Rejected)
            return Conflict("Предложението е отхвърлено и не може да бъде одобрено.");

        var aiReview = await _reviewService.EvaluateAsync(post, cancellationToken);
        var reviewerName = User.FindFirstValue(ClaimTypes.Name) ?? "panel";

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

        _logger.LogInformation("Trail proposal {PostId} approved by {Reviewer}. New trail id: {TrailId}.", postId, reviewerName, trail.Id);
        return Ok(new { trailId = trail.Id, trailName = trail.Name });
    }

    [HttpPost("{postId:int}/reject")]
    [Authorize(Roles = "AdminPanel")]
    public async Task<IActionResult> RejectProposal(
        int postId,
        [FromBody] CommunityPostRejectRequest request,
        CancellationToken cancellationToken)
    {
        var post = await _dbContext.CommunityTrailPosts
            .FirstOrDefaultAsync(item => item.Id == postId, cancellationToken);

        if (post is null) return NotFound();

        if (post.PostType != CommunityPostType.TrailProposal)
            return BadRequest("Постът не е предложение за пътека.");

        if (post.ProposalStatus == ProposalStatus.Approved)
            return Conflict("Одобреното предложение не може да бъде отхвърлено.");

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 4)
            return BadRequest("Причината за отхвърляне трябва да е поне 4 символа.");

        var reviewerName = User.FindFirstValue(ClaimTypes.Name) ?? "panel";

        post.ProposalStatus = ProposalStatus.Rejected;
        post.RejectionReason = reason;
        post.ReviewedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trail proposal {PostId} rejected by {Reviewer}. Reason: {Reason}.", postId, reviewerName, reason);
        return NoContent();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

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

    private string CreatePanelToken(string username)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "AdminPanel"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool VerifyCredentials(string? inputUsername, string? inputPassword)
    {
        // Use HMAC to normalize lengths before constant-time comparison
        var secretKey = Encoding.UTF8.GetBytes("panel_cred_verify_key_v1");
        var expectedHash = HMACSHA256.HashData(
            secretKey,
            Encoding.UTF8.GetBytes($"{_adminPanelOptions.Username}\0{_adminPanelOptions.Password}"));
        var inputHash = HMACSHA256.HashData(
            secretKey,
            Encoding.UTF8.GetBytes($"{inputUsername ?? ""}\0{inputPassword ?? ""}"));

        return CryptographicOperations.FixedTimeEquals(expectedHash, inputHash);
    }

    private static TrailDifficultyLevel ParseDifficultyLevel(string? value)
    {
        if (Enum.TryParse<TrailDifficultyLevel>(value, ignoreCase: true, out var parsed))
            return parsed;
        return TrailDifficultyLevel.Moderate;
    }

    private static int ToLegacyDifficulty(TrailDifficultyLevel level) => level switch
    {
        TrailDifficultyLevel.Easy => 2,
        TrailDifficultyLevel.Moderate => 3,
        TrailDifficultyLevel.Difficult => 4,
        _ => 3,
    };
}

public sealed class AdminPanelLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
