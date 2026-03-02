using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using EcoTrails.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrailsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly OpenRouteService _openRouteService;

        public TrailsController(AppDbContext context, OpenRouteService openRouteService)
        {
            _context = context;
            _openRouteService = openRouteService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<Trail>>> GetTrails(
            [FromQuery] string? search,
            [FromQuery] int? difficulty,
            [FromQuery] bool onlyWithCoords = false,
            [FromQuery] double? minDuration = null,
            [FromQuery] double? maxDuration = null,
            [FromQuery] int? minElevation = null,
            [FromQuery] int? maxElevation = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDirection = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Trails.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(trail =>
                    trail.Name.Contains(search) ||
                    trail.Location.Contains(search));
            }

            if (difficulty.HasValue)
            {
                query = query.Where(trail => trail.Difficulty == difficulty.Value);
            }

            if (onlyWithCoords)
            {
                query = query.Where(trail => trail.Latitude.HasValue && trail.Longitude.HasValue);
            }

            if (minDuration.HasValue)
            {
                query = query.Where(trail => trail.DurationInHours >= minDuration.Value);
            }

            if (maxDuration.HasValue)
            {
                query = query.Where(trail => trail.DurationInHours <= maxDuration.Value);
            }

            if (minElevation.HasValue)
            {
                query = query.Where(trail => trail.ElevationGain >= minElevation.Value);
            }

            if (maxElevation.HasValue)
            {
                query = query.Where(trail => trail.ElevationGain <= maxElevation.Value);
            }

            var totalCount = await query.CountAsync();

            var orderedQuery = ApplySorting(query, sortBy, sortDirection);

            var trails = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";

            return Ok(new PagedResponse<Trail>
            {
                Items = trails,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Trail>> PostTrail(Trail trail)
        {
            _context.Trails.Add(trail);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTrails), new { id = trail.Id }, trail);
        }

        [HttpGet("export")]
        public async Task<ActionResult<IEnumerable<Trail>>> ExportTrails(
            [FromQuery] string? search,
            [FromQuery] int? difficulty,
            [FromQuery] bool onlyWithCoords = false,
            [FromQuery] double? minDuration = null,
            [FromQuery] double? maxDuration = null,
            [FromQuery] int? minElevation = null,
            [FromQuery] int? maxElevation = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDirection = null,
            [FromQuery] string? ids = null)
        {
            var query = _context.Trails.AsNoTracking().AsQueryable();

            var hasIdsFilter = !string.IsNullOrWhiteSpace(ids);
            if (hasIdsFilter)
            {
                var idList = ids!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => int.TryParse(value, out var parsedId) ? parsedId : 0)
                    .Where(parsedId => parsedId > 0)
                    .Distinct()
                    .ToList();

                if (idList.Count == 0)
                {
                    return Ok(Array.Empty<Trail>());
                }

                query = query.Where(trail => idList.Contains(trail.Id));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(trail =>
                    trail.Name.Contains(search) ||
                    trail.Location.Contains(search));
            }

            if (difficulty.HasValue)
            {
                query = query.Where(trail => trail.Difficulty == difficulty.Value);
            }

            if (onlyWithCoords)
            {
                query = query.Where(trail => trail.Latitude.HasValue && trail.Longitude.HasValue);
            }

            if (minDuration.HasValue)
            {
                query = query.Where(trail => trail.DurationInHours >= minDuration.Value);
            }

            if (maxDuration.HasValue)
            {
                query = query.Where(trail => trail.DurationInHours <= maxDuration.Value);
            }

            if (minElevation.HasValue)
            {
                query = query.Where(trail => trail.ElevationGain >= minElevation.Value);
            }

            if (maxElevation.HasValue)
            {
                query = query.Where(trail => trail.ElevationGain <= maxElevation.Value);
            }

            var items = await ApplySorting(query, sortBy, sortDirection)
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Trail>> GetTrail(int id)
        {
            var trail = await _context.Trails.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
            if (trail is null)
            {
                return NotFound();
            }

            return Ok(trail);
        }

        [HttpGet("{id:int}/route")]
        public async Task<ActionResult<object>> GetTrailRoute(int id, CancellationToken cancellationToken)
        {
            var trail = await _context.Trails.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (trail is null)
            {
                return NotFound();
            }

            if (!trail.Latitude.HasValue || !trail.Longitude.HasValue)
            {
                return BadRequest("Trail does not have start coordinates.");
            }

            var startLatitude = trail.Latitude.Value;
            var startLongitude = trail.Longitude.Value;
            var (endLatitude, endLongitude) = EstimateEndPoint(trail);

            var externalRoute = await _openRouteService.GetHikingRouteAsync(
                (startLatitude, startLongitude),
                (endLatitude, endLongitude),
                cancellationToken);

            var coordinates = externalRoute?.Select(point => new[] { point.Latitude, point.Longitude }).ToList()
                ??
                new List<double[]>
                {
                    new[] { startLatitude, startLongitude },
                    new[] { endLatitude, endLongitude }
                };

            return Ok(new
            {
                StartLatitude = startLatitude,
                StartLongitude = startLongitude,
                EndLatitude = endLatitude,
                EndLongitude = endLongitude,
                IsEstimatedEnd = true,
                IsExternalRoute = externalRoute is not null,
                Coordinates = coordinates
            });
        }

        private static (double EndLatitude, double EndLongitude) EstimateEndPoint(Trail trail)
        {
            var startLatitude = trail.Latitude ?? 0;
            var startLongitude = trail.Longitude ?? 0;

            var seedRadians = ((trail.Id % 360) * Math.PI) / 180;
            var distanceFactor = Math.Min(Math.Max(trail.DurationInHours, 1), 6) * 0.01;
            var latOffset = Math.Sin(seedRadians) * distanceFactor;
            var lngOffset = Math.Cos(seedRadians) * distanceFactor;

            return (
                Math.Round(startLatitude + latOffset, 6),
                Math.Round(startLongitude + lngOffset, 6));
        }

        private static IQueryable<Trail> ApplySorting(
            IQueryable<Trail> query,
            string? sortBy,
            string? sortDirection)
        {
            var isDesc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();

            return normalizedSortBy switch
            {
                "name" => isDesc ? query.OrderByDescending(trail => trail.Name) : query.OrderBy(trail => trail.Name),
                "difficulty" => isDesc ? query.OrderByDescending(trail => trail.Difficulty) : query.OrderBy(trail => trail.Difficulty),
                "duration" => isDesc ? query.OrderByDescending(trail => trail.DurationInHours) : query.OrderBy(trail => trail.DurationInHours),
                "elevation" => isDesc ? query.OrderByDescending(trail => trail.ElevationGain) : query.OrderBy(trail => trail.ElevationGain),
                _ => query.OrderBy(trail => trail.Id),
            };
        }
    }
}