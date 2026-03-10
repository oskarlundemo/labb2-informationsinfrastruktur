namespace PlacementService.Api.Options;

public sealed class JobSearchOptions
{
    public const string SectionName = "JobSearch";
    public string BaseUrl { get; set; } = "https://jobsearch.api.jobtechdev.se";
    public string UserAgent { get; set; } = "PlacementService.Api/1.0 (edu)";
}
