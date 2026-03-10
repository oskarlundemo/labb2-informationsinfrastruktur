namespace PlacementService.Api.Options;

public sealed class ScbPxWebOptions
{
    public const string SectionName = "ScbPxWeb";
    public string TableUrl { get; set; } = "https://statistikdatabasen.scb.se/api/v2/tables/TAB5932";
    public string UserAgent { get; set; } = "PlacementService.Api/1.0 (edu)";
    public string ContentCode { get; set; } = "000007CD";
    public string SectorCode { get; set; } = "0";
    public string GenderCode { get; set; } = "1+2";
    public int DefaultYear { get; set; } = DateTimeOffset.UtcNow.Year - 1;
    public int CacheMinutes { get; set; } = 25;
    public bool NormalizeSsykTo3Digits { get; set; } = false;
    public string SourceName { get; set; } = "SCB PxWeb API (v2)";
}
