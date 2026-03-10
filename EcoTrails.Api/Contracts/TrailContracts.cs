namespace EcoTrails.Api.Contracts;

public class TrailQueryParameters
{
    public string? Search { get; set; }
    public int? Difficulty { get; set; }
    public bool OnlyWithCoords { get; set; }
    public double? MinDuration { get; set; }
    public double? MaxDuration { get; set; }
    public int? MinElevation { get; set; }
    public int? MaxElevation { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class TrailIndexViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
}
