namespace PlacementService.Ui.Models;

public sealed record OccupationSummaryItem(
    string? OccupationLabel,
    string? OccupationSsyk,
    int AdsCount,
    SalaryInfo? Salary
);
