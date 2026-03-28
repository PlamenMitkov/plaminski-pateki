using System.Security.Claims;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoTrails.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class FavoritesController(IFavoritesRepository favoritesRepository) : ControllerBase
{

    [HttpGet]
    public async Task<ActionResult<IEnumerable<int>>> GetFavorites(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var trailIds = await favoritesRepository.GetFavoriteTrailIdsAsync(userId, cancellationToken);

        return Ok(trailIds);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<IEnumerable<int>>> SyncFavorites(FavoritesSyncRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var validTrailIds = await favoritesRepository.SyncFavoritesAsync(
            userId,
            request.TrailIds,
            cancellationToken);

        return Ok(validTrailIds);
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");
    }
}