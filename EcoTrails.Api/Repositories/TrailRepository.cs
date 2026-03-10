using EcoTrails.Api.Contracts;
using EcoTrails.Api.Data;
using EcoTrails.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EcoTrails.Api.Repositories;

public class TrailRepository : ITrailRepository
{
    private readonly AppDbContext _context;

    public TrailRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResponse<Trail>> GetPagedTrailsAsync(
        TrailQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePaging(parameters.Page, parameters.PageSize);
        var query = BuildFilteredQuery(parameters);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ApplySorting(query, parameters.SortBy, parameters.SortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<Trail>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<Trail>> ExportTrailsAsync(
        TrailQueryParameters parameters,
        string? ids,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFilteredQuery(parameters);

        if (!string.IsNullOrWhiteSpace(ids))
        {
            var idList = ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var parsedId) ? parsedId : 0)
                .Where(parsedId => parsedId > 0)
                .Distinct()
                .ToList();

            if (idList.Count == 0)
            {
                return [];
            }

            query = query.Where(trail => idList.Contains(trail.Id));
        }

        return await ApplySorting(query, parameters.SortBy, parameters.SortDirection)
            .ToListAsync(cancellationToken);
    }

    public async Task<Trail?> GetTrailByIdAsync(int id, bool asNoTracking = true, CancellationToken cancellationToken = default)
    {
        var query = asNoTracking ? _context.Trails.AsNoTracking() : _context.Trails;
        return await query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task AddTrailAsync(Trail trail, CancellationToken cancellationToken = default)
    {
        await _context.Trails.AddAsync(trail, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Trail> BuildFilteredQuery(TrailQueryParameters parameters)
    {
        var query = _context.Trails.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            query = query.Where(trail =>
                trail.Name.Contains(parameters.Search) ||
                trail.Location.Contains(parameters.Search));
        }

        if (parameters.Difficulty.HasValue)
        {
            query = query.Where(trail => trail.Difficulty == parameters.Difficulty.Value);
        }

        if (parameters.OnlyWithCoords)
        {
            query = query.Where(trail => trail.Latitude.HasValue && trail.Longitude.HasValue);
        }

        if (parameters.MinDuration.HasValue)
        {
            query = query.Where(trail => trail.DurationInHours >= parameters.MinDuration.Value);
        }

        if (parameters.MaxDuration.HasValue)
        {
            query = query.Where(trail => trail.DurationInHours <= parameters.MaxDuration.Value);
        }

        if (parameters.MinElevation.HasValue)
        {
            query = query.Where(trail => trail.ElevationGain >= parameters.MinElevation.Value);
        }

        if (parameters.MaxElevation.HasValue)
        {
            query = query.Where(trail => trail.ElevationGain <= parameters.MaxElevation.Value);
        }

        return query;
    }

    private static IQueryable<Trail> ApplySorting(IQueryable<Trail> query, string? sortBy, string? sortDirection)
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

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        return (normalizedPage, normalizedPageSize);
    }
}
