using System.Security.Claims;
using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _context;

    public FavoritesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<int>>> GetFavorites()
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var trailIds = await _context.UserFavoriteTrails
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.TrailId)
            .OrderBy(item => item)
            .ToListAsync();

        return Ok(trailIds);
    }

    [HttpPost("sync")]
    public async Task<ActionResult<IEnumerable<int>>> SyncFavorites(FavoritesSyncRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var requestedIds = request.TrailIds
            .Where(item => item > 0)
            .Distinct()
            .ToHashSet();

        var validTrailIds = await _context.Trails
            .AsNoTracking()
            .Where(item => requestedIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync();

        var existing = await _context.UserFavoriteTrails
            .Where(item => item.UserId == userId)
            .ToListAsync();

        _context.UserFavoriteTrails.RemoveRange(existing);

        var newFavorites = validTrailIds
            .Select(trailId => new UserFavoriteTrail
            {
                UserId = userId,
                TrailId = trailId,
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();

        await _context.UserFavoriteTrails.AddRangeAsync(newFavorites);
        await _context.SaveChangesAsync();

        return Ok(validTrailIds.OrderBy(item => item));
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("sub");
    }
}