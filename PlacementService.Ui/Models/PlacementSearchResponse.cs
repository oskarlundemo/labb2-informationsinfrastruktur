namespace PlacementService.Ui.Models;

public sealed record PlacementSearchResponse(
    int Offset,
    int Limit,
    int Total,
    IReadOnlyList<PlacementItem> Items
);
