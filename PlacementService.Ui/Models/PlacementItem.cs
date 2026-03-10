namespace PlacementService.Ui.Models;

public sealed record PlacementItem(
    string Id,
    string? Headline,
    string? Employer,
    string? Region,
    string? Municipality,
    string? OccupationLabel,
    string? OccupationSsyk,
    DateTimeOffset? PublishedAt,
    SalaryInfo? Salary
);
