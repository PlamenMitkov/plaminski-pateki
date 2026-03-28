using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using EcoTrails.Api.Repositories;
using EcoTrails.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace EcoTrails.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrailsController : ControllerBase
    {
        private readonly ITrailRepository _trailRepository;
        private readonly OpenRouteService _openRouteService;
        private readonly ITrailOfflineEnrichmentService _trailOfflineEnrichmentService;
        private readonly AppDbContext _dbContext;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan TrailsCacheDuration = TimeSpan.FromMinutes(10);
        private const string TrailsCacheVersionKey = "trails-cache-version";

        public TrailsController(
            ITrailRepository trailRepository,
            OpenRouteService openRouteService,
            ITrailOfflineEnrichmentService trailOfflineEnrichmentService,
            AppDbContext dbContext,
            IMemoryCache cache)
        {
            _trailRepository = trailRepository;
            _openRouteService = openRouteService;
            _trailOfflineEnrichmentService = trailOfflineEnrichmentService;
            _dbContext = dbContext;
            _cache = cache;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
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
            var cacheVersion = GetCacheVersion();
            var cacheKey = BuildPagedTrailsCacheKey(
                "full",
                cacheVersion,
                search,
                difficulty,
                onlyWithCoords,
                minDuration,
                maxDuration,
                minElevation,
                maxElevation,
                sortBy,
                sortDirection,
                page,
                pageSize);

            var etag = BuildEtag(cacheKey);
            if (RequestHasMatchingEtag(etag))
            {
                ApplyPublicCacheHeaders(etag, exposeTotalCount: false);
                return StatusCode(StatusCodes.Status304NotModified);
            }

            PagedResponse<Trail> result;
            if (!_cache.TryGetValue(cacheKey, out PagedResponse<Trail>? cachedResult) || cachedResult is null)
            {
                result = await _trailRepository.GetPagedTrailsAsync(new TrailQueryParameters
                {
                    Search = search,
                    Difficulty = difficulty,
                    OnlyWithCoords = onlyWithCoords,
                    MinDuration = minDuration,
                    MaxDuration = maxDuration,
                    MinElevation = minElevation,
                    MaxElevation = maxElevation,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    Page = page,
                    PageSize = pageSize
                });

                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TrailsCacheDuration
                });
            }
            else
            {
                result = cachedResult;
            }

            Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
            ApplyPublicCacheHeaders(etag, exposeTotalCount: true);

            return Ok(result);
        }

        [HttpGet("summary")]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        public async Task<ActionResult<PagedResponse<TrailIndexViewModel>>> GetTrailsSummary(
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
            var cacheVersion = GetCacheVersion();
            var cacheKey = BuildPagedTrailsCacheKey(
                "summary",
                cacheVersion,
                search,
                difficulty,
                onlyWithCoords,
                minDuration,
                maxDuration,
                minElevation,
                maxElevation,
                sortBy,
                sortDirection,
                page,
                pageSize);

            var etag = BuildEtag(cacheKey);
            if (RequestHasMatchingEtag(etag))
            {
                ApplyPublicCacheHeaders(etag, exposeTotalCount: false);
                return StatusCode(StatusCodes.Status304NotModified);
            }

            PagedResponse<TrailIndexViewModel> response;
            if (!_cache.TryGetValue(cacheKey, out PagedResponse<TrailIndexViewModel>? cachedResponse) || cachedResponse is null)
            {
                var trails = await _trailRepository.GetPagedTrailsAsync(new TrailQueryParameters
                {
                    Search = search,
                    Difficulty = difficulty,
                    OnlyWithCoords = onlyWithCoords,
                    MinDuration = minDuration,
                    MaxDuration = maxDuration,
                    MinElevation = minElevation,
                    MaxElevation = maxElevation,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    Page = page,
                    PageSize = pageSize
                });

                var viewModel = trails.Items.Select(trail => new TrailIndexViewModel
                {
                    Id = trail.Id,
                    Title = trail.Name,
                    Difficulty = trail.DifficultyLevel.ToString(),
                    Region = trail.Region,
                    ShortDescription = BuildShortDescription(trail.Description)
                });

                response = new PagedResponse<TrailIndexViewModel>
                {
                    Items = viewModel,
                    TotalCount = trails.TotalCount,
                    Page = trails.Page,
                    PageSize = trails.PageSize
                };

                _cache.Set(cacheKey, response, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TrailsCacheDuration
                });
            }
            else
            {
                response = cachedResponse;
            }

            ApplyPublicCacheHeaders(etag, exposeTotalCount: false);
            return Ok(response);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Trail>> PostTrail(Trail trail)
        {
            await _trailRepository.AddTrailAsync(trail);
            await _trailRepository.SaveChangesAsync();
            BumpCacheVersion();

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
            var items = await _trailRepository.ExportTrailsAsync(
                new TrailQueryParameters
                {
                    Search = search,
                    Difficulty = difficulty,
                    OnlyWithCoords = onlyWithCoords,
                    MinDuration = minDuration,
                    MaxDuration = maxDuration,
                    MinElevation = minElevation,
                    MaxElevation = maxElevation,
                    SortBy = sortBy,
                    SortDirection = sortDirection
                },
                ids);

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Trail>> GetTrail(int id)
        {
            var trail = await _trailRepository.GetTrailByIdAsync(id);
            if (trail is null)
            {
                return NotFound();
            }

            return Ok(trail);
        }

        [HttpGet("{id:int}/route")]
        public async Task<ActionResult<object>> GetTrailRoute(int id, CancellationToken cancellationToken)
        {
            var trail = await _trailRepository.GetTrailByIdAsync(id, cancellationToken: cancellationToken);
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

        [HttpGet("offline-enrichment")]
        public async Task<ActionResult<TrailOfflineEnrichmentResponse>> GetOfflineEnrichment(
            [FromQuery] string? ids,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return BadRequest("Query parameter 'ids' is required.");
            }

            var idList = ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var parsedId) ? parsedId : 0)
                .Where(parsedId => parsedId > 0)
                .Distinct()
                .Take(250)
                .ToArray();

            if (idList.Length == 0)
            {
                return BadRequest("Query parameter 'ids' must contain at least one valid trail id.");
            }

            var trails = await _trailRepository.ExportTrailsAsync(
                new TrailQueryParameters(),
                string.Join(',', idList),
                cancellationToken);

            var enrichment = await _trailOfflineEnrichmentService.GetOfflineEnrichmentAsync(trails, cancellationToken);
            return Ok(enrichment);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/data-quality")]
        public async Task<ActionResult<TrailDataQualityResponse>> GetDataQuality(CancellationToken cancellationToken)
        {
            var trails = await _dbContext.Trails.AsNoTracking().ToListAsync(cancellationToken);
            var staleCutoff = DateTime.UtcNow.AddHours(-24);
            var staleSourcePreviewCount = 0;
            try
            {
                staleSourcePreviewCount = await _dbContext.TrailEnrichmentSnapshots
                    .AsNoTracking()
                    .Where(item => item.SourcePreviewFetchedAtUtc == null || item.SourcePreviewFetchedAtUtc < staleCutoff)
                    .Select(item => item.TrailId)
                    .Distinct()
                    .CountAsync(cancellationToken);
            }
            catch
            {
                staleSourcePreviewCount = trails.Count;
            }

            var response = new TrailDataQualityResponse
            {
                TotalTrails = trails.Count,
                MissingCoordinates = trails.Count(item => !item.Latitude.HasValue || !item.Longitude.HasValue),
                MissingLengthHints = trails.Count(item => item.DurationInHours <= 0),
                MissingElevationGain = trails.Count(item => item.ElevationGain <= 0),
                MissingDescription = trails.Count(item => string.IsNullOrWhiteSpace(item.Description)),
                StaleSourcePreviews = staleSourcePreviewCount,
                GeneratedAtUtc = DateTime.UtcNow,
            };

            return Ok(response);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/re-enrich")]
        public async Task<ActionResult<object>> TriggerReEnrichment()
        {
            await _trailOfflineEnrichmentService.WarmDailyCacheAsync();
            return Ok(new
            {
                Status = "started",
                Message = "Manual re-enrichment completed for warmup batch."
            });
        }

        private static string BuildShortDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return string.Empty;
            }

            const int maxLength = 100;
            if (description.Length <= maxLength)
            {
                return description;
            }

            return $"{description[..maxLength].TrimEnd()}...";
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

        private int GetCacheVersion()
        {
            if (!_cache.TryGetValue(TrailsCacheVersionKey, out int version))
            {
                version = 1;
                _cache.Set(TrailsCacheVersionKey, version, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
                });
            }

            return version;
        }

        private void BumpCacheVersion()
        {
            var nextVersion = GetCacheVersion() + 1;
            _cache.Set(TrailsCacheVersionKey, nextVersion, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
            });
        }

        private static string BuildPagedTrailsCacheKey(
            string scope,
            int version,
            string? search,
            int? difficulty,
            bool onlyWithCoords,
            double? minDuration,
            double? maxDuration,
            int? minElevation,
            int? maxElevation,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize)
        {
            return string.Join('|',
                "trails",
                scope,
                $"v{version}",
                search?.Trim().ToLowerInvariant() ?? string.Empty,
                difficulty?.ToString() ?? string.Empty,
                onlyWithCoords ? "1" : "0",
                minDuration?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                maxDuration?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                minElevation?.ToString() ?? string.Empty,
                maxElevation?.ToString() ?? string.Empty,
                sortBy?.Trim().ToLowerInvariant() ?? string.Empty,
                sortDirection?.Trim().ToLowerInvariant() ?? string.Empty,
                page.ToString(),
                pageSize.ToString());
        }

        private static string BuildEtag(string cacheKey)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
            return $"\"{Convert.ToHexString(hash)}\"";
        }

        private bool RequestHasMatchingEtag(string etag)
        {
            if (!Request.Headers.TryGetValue("If-None-Match", out var values))
            {
                return false;
            }

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tags.Any(tag => string.Equals(tag, etag, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyPublicCacheHeaders(string etag, bool exposeTotalCount)
        {
            Response.Headers["Cache-Control"] = "public,max-age=60";
            Response.Headers["ETag"] = etag;
            Response.Headers["Access-Control-Expose-Headers"] = exposeTotalCount
                ? "X-Total-Count,ETag"
                : "ETag";
        }

    }
}