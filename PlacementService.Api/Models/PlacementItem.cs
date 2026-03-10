namespace PlacementService.Api.Models;

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
    // TODO: add additional fields (e.g. ContractType, OccupationDescription, AdvertisementUrl) if you decide to display more data in the UI. NOTE: You will need to update both the API and UI models and mapping code accordingly.
);
