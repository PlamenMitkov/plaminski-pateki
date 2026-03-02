using EcoTrails.Api.Contracts;
using EcoTrails.Api.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace EcoTrails.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("assistant")]
public class AssistantController : ControllerBase
{
    private readonly IOpenAiAssistantService _assistantService;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(
        IOpenAiAssistantService assistantService,
        ILogger<AssistantController> logger)
    {
        _assistantService = assistantService;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("sessions/mine")]
    public async Task<ActionResult<List<AssistantSessionResponse>>> GetMySessions(
        [FromQuery] int limit = 12,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var response = await _assistantService.GetUserSessionsAsync(userId, limit, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("sessions")]
    public async Task<ActionResult<AssistantSessionResponse>> CreateSession(
        [FromBody] AssistantSessionCreateRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var response = await _assistantService.CreateSessionAsync(
            request ?? new AssistantSessionCreateRequest(),
            userId,
            cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<ActionResult<List<AssistantSessionMessageResponse>>> GetSessionMessages(
        [FromRoute] string sessionId,
        [FromQuery] int limit = 80,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var response = await _assistantService.GetSessionMessagesAsync(sessionId, userId, limit, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(
        [FromRoute] string sessionId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var deleted = await _assistantService.DeleteSessionAsync(sessionId, userId, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("chat")]
    public async Task<ActionResult<AssistantChatResponse>> Chat(
        [FromBody] AssistantChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _assistantService.GenerateReplyAsync(request, userId, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Assistant request failed due to configuration or provider response.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Assistant is temporarily unavailable.");
        }
    }

    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("assistant-enrich")]
    [HttpPost("enrich")]
    public async Task<ActionResult<AssistantEnrichResponse>> Enrich(
        [FromBody] AssistantEnrichRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _assistantService.EnrichTrailsAsync(request ?? new AssistantEnrichRequest(), cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Assistant enrichment failed due to configuration or provider response.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Assistant enrichment is temporarily unavailable.");
        }
    }

    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("assistant-enrich")]
    [HttpPost("vector/index")]
    public IActionResult IndexVectors([FromBody] AssistantVectorIndexRequest? request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var indexRequest = request ?? new AssistantVectorIndexRequest();
        var jobId = BackgroundJob.Enqueue<IOpenAiAssistantService>(
            service => service.IndexTrailsAsync(indexRequest, CancellationToken.None));

        return Accepted(new { JobId = jobId });
    }

    [Authorize]
    [HttpPost("vector/search")]
    public async Task<ActionResult<AssistantVectorSearchResponse>> VectorSearch(
        [FromBody] AssistantVectorSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _assistantService.SearchSimilarTrailsAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Vector search failed due to configuration or provider response.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Vector search is temporarily unavailable.");
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");
    }
}