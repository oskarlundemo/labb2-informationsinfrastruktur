using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using PlacementService.Api.Models;
using PlacementService.Api.Options;

namespace PlacementService.Api.Services;

public sealed class JobSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly JobSearchOptions _options;
    private readonly ILogger<JobSearchClient> _logger;

    public JobSearchClient(
        HttpClient httpClient,
        IOptions<JobSearchOptions> options,
        ILogger<JobSearchClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JobSearchResult> SearchAsync(string query, string? region, int limit, int offset, CancellationToken cancellationToken)
    {
        var queryParams = new Dictionary<string, string?>
        {
            ["q"] = query,
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString()
        };

        if (!string.IsNullOrWhiteSpace(region))
        {
            queryParams["region"] = region;
        }

        var relativeUrl = QueryHelpers.AddQueryString("search", queryParams);

        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new JobSearchResult(offset, limit, 0, Array.Empty<PlacementItem>());
        }

        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("JobSearch API returned {StatusCode}: {Payload}", response.StatusCode, payload);
            throw new HttpRequestException($"JobSearch API returned {response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var total = ReadTotal(root);
        var rawItems = ReadItems(root);
        var items = rawItems
            .Select(item => new PlacementItem(
                item.Id,
                item.Headline,
                item.Employer,
                item.Region,
                item.Municipality,
                item.OccupationLabel,
                item.OccupationSsyk,
                item.PublishedAt,
                null))
            .ToList();

        return new JobSearchResult(offset, limit, total, items);
    }

    private static int ReadTotal(JsonElement root)
    {
        if (root.TryGetProperty("total", out var totalElement))
        {
            if (totalElement.ValueKind == JsonValueKind.Number && totalElement.TryGetInt32(out var number))
            {
                return number;
            }

            if (totalElement.ValueKind == JsonValueKind.Object && totalElement.TryGetProperty("value", out var valueElement)
                && valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt32(out var valueNumber))
            {
                return valueNumber;
            }
        }

        return 0;
    }

    private static IReadOnlyList<JobSearchRawPlacement> ReadItems(JsonElement root)
    {
        if (root.TryGetProperty("hits", out var hitsElement) && hitsElement.ValueKind == JsonValueKind.Array)
        {
            return hitsElement.EnumerateArray().Select(MapPlacement).Where(item => item is not null).Select(item => item!).ToList();
        }

        if (root.TryGetProperty("positions", out var positionsElement) && positionsElement.ValueKind == JsonValueKind.Array)
        {
            return positionsElement.EnumerateArray().Select(MapPlacement).Where(item => item is not null).Select(item => item!).ToList();
        }

        return Array.Empty<JobSearchRawPlacement>();
    }

    private static JobSearchRawPlacement? MapPlacement(JsonElement hit)
    {
        var id = JsonHelpers.GetString(hit, "id") ?? JsonHelpers.GetString(hit, "ad_id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var headline = FirstNonEmpty(
            JsonHelpers.GetString(hit, "headline"),
            JsonHelpers.GetString(hit, "title"));

        var employer = FirstNonEmpty(
            JsonHelpers.GetString(hit, "employer", "name"),
            JsonHelpers.GetString(hit, "employer_name"));

        var region = FirstNonEmpty(
            JsonHelpers.GetString(hit, "workplace_address", "region"),
            JsonHelpers.GetString(hit, "region"));

        var municipality = FirstNonEmpty(
            JsonHelpers.GetString(hit, "workplace_address", "municipality"),
            JsonHelpers.GetString(hit, "municipality"));

        var occupationLabel = FirstNonEmpty(
            JsonHelpers.GetString(hit, "occupation", "label"),
            JsonHelpers.GetString(hit, "occupation", "0", "label"),
            JsonHelpers.GetString(hit, "occupation_group", "label"),
            JsonHelpers.GetString(hit, "occupation_group", "0", "label"),
            JsonHelpers.GetString(hit, "occupation_field", "label"),
            JsonHelpers.GetString(hit, "occupation_field", "0", "label"));

        var occupationSsyk = FirstNonEmpty(
            // Group-level taxonomy usually aligns better with SCB salary tables, hence the group first.
            JsonHelpers.GetString(hit, "occupation_group", "legacy_ams_taxonomy_id"),
            JsonHelpers.GetString(hit, "occupation_group", "0", "legacy_ams_taxonomy_id"),
            JsonHelpers.GetString(hit, "occupation", "legacy_ams_taxonomy_id"),
            JsonHelpers.GetString(hit, "occupation", "0", "legacy_ams_taxonomy_id"),
            JsonHelpers.GetString(hit, "legacy_ams_taxonomy_id"),
            JsonHelpers.GetString(hit, "occupation_field", "legacy_ams_taxonomy_id"),
            JsonHelpers.GetString(hit, "occupation_field", "0", "legacy_ams_taxonomy_id"),
            JsonHelpers.GetString(hit, "occupation", "code"),
            JsonHelpers.GetString(hit, "occupation", "0", "code"),
            JsonHelpers.GetString(hit, "occupation_group", "code"),
            JsonHelpers.GetString(hit, "occupation_group", "0", "code"),
            JsonHelpers.GetString(hit, "occupation_field", "code"),
            JsonHelpers.GetString(hit, "occupation_field", "0", "code"));

        var publishedAt = JsonHelpers.GetDateTimeOffset(hit, "publication_date")
            ?? JsonHelpers.GetDateTimeOffset(hit, "published");

        // TODO: extract any additional fields you need (for example "employment_type", "description" or "application_url") from the JSON and include them in the JobSearchRawPlacement below and PlacementItem.

        return new JobSearchRawPlacement(
            id,
            headline,
            employer,
            region,
            municipality,
            occupationLabel,
            occupationSsyk,
            publishedAt);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed record JobSearchRawPlacement(
        string Id,
        string? Headline,
        string? Employer,
        string? Region,
        string? Municipality,
        string? OccupationLabel,
        string? OccupationSsyk,
        DateTimeOffset? PublishedAt);
}

public sealed record JobSearchResult(int Offset, int Limit, int Total, IReadOnlyList<PlacementItem> Items);
