namespace PlacementService.Api.Models;

public sealed record PlacementSummaryResponse(
    string Query,
    string? Region,
    int Offset,
    int Limit,
    int Total,
    IReadOnlyList<OccupationSummaryItem> Occupations
);
