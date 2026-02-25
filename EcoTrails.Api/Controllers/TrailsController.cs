using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrailsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TrailsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<Trail>>> GetTrails(
            [FromQuery] string? search,
            [FromQuery] int? difficulty,
            [FromQuery] bool onlyWithCoords = false,
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

            var totalCount = await query.CountAsync();

            var trails = await query
                .OrderBy(trail => trail.Id)
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

            var items = await query
                .OrderBy(trail => trail.Id)
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
    }
}