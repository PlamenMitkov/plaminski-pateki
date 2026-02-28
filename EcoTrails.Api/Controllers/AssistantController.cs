using EcoTrails.Api.Contracts;
using EcoTrails.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EcoTrails.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AssistantController : ControllerBase
{
    private readonly OpenAiAssistantService _assistantService;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(OpenAiAssistantService assistantService, ILogger<AssistantController> logger)
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

    [AllowAnonymous]
    [HttpPost("sessions")]
    public async Task<ActionResult<AssistantSessionResponse>> CreateSession(
        [FromBody] AssistantSessionCreateRequest? request,
        CancellationToken cancellationToken)
    {
        var response = await _assistantService.CreateSessionAsync(
            request ?? new AssistantSessionCreateRequest(),
            GetCurrentUserId(),
            cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<ActionResult<List<AssistantSessionMessageResponse>>> GetSessionMessages(
        [FromRoute] string sessionId,
        [FromQuery] int limit = 80,
        CancellationToken cancellationToken = default)
    {
        var response = await _assistantService.GetSessionMessagesAsync(sessionId, GetCurrentUserId(), limit, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(
        [FromRoute] string sessionId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _assistantService.DeleteSessionAsync(sessionId, GetCurrentUserId(), cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("chat")]
    public async Task<ActionResult<AssistantChatResponse>> Chat(
        [FromBody] AssistantChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        try
        {
            var response = await _assistantService.GenerateReplyAsync(request, GetCurrentUserId(), cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Assistant request failed due to configuration or provider response.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Assistant is temporarily unavailable.");
        }
    }

    [AllowAnonymous]
    [HttpPost("enrich")]
    public async Task<ActionResult<AssistantEnrichResponse>> Enrich(
        [FromBody] AssistantEnrichRequest? request,
        CancellationToken cancellationToken)
    {
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

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");
    }
}