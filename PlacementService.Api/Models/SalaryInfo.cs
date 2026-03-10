namespace PlacementService.Api.Models;

public sealed record SalaryInfo(
    string Ssyk,
    int Year,
    decimal? MonthlySalarySek,
    string Source
);
